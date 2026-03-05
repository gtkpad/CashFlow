using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.IntegrationTests.Infrastructure;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace CashFlow.IntegrationTests.Consolidation;

/// <summary>
/// Tests that exercise concurrency scenarios using real PostgreSQL (required for xmin row version).
/// The existing <see cref="TransactionCreatedConsumerTests"/> use InMemoryDatabase which cannot
/// detect optimistic concurrency conflicts — these tests fill that gap.
/// </summary>
public class TransactionCreatedConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();

        services.AddDbContext<ConsolidationDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString()));

        services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        services.AddSingleton(Substitute.For<IOutputCacheStore>());
        services.AddLogging();

        // Register consumer WITH definition to activate retry/partitioner.
        // Include EF outbox configuration so the definition's UseEntityFrameworkOutbox works.
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<TransactionCreatedConsumer, TransactionCreatedConsumerDefinition>();
            cfg.AddEntityFrameworkOutbox<ConsolidationDbContext>(o =>
            {
                o.UsePostgres();
            });
        });

        _provider = services.BuildServiceProvider();

        // Run migrations to create tables (including xmin row version and outbox tables)
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        await db.Database.MigrateAsync();

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    /// <summary>
    /// Validates that PostgreSQL xmin-based optimistic concurrency detects
    /// conflicting updates on the same DailySummary row.
    /// This mechanism is what makes the retry middleware in
    /// TransactionCreatedConsumerDefinition meaningful.
    /// </summary>
    [Fact]
    public async Task ConcurrentUpdate_ShouldDetectXminConflict()
    {
        var merchantId = Guid.NewGuid();
        var strongMerchantId = new MerchantId(merchantId);
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-10);

        // Seed a DailySummary using context 1
        using var scope1 = _provider.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

        var summary = DailySummary.CreateForDay(strongMerchantId, date);
        summary.ApplyTransaction(TransactionType.Credit, new Money(100m));
        db1.DailySummaries.Add(summary);
        await db1.SaveChangesAsync();

        // Context 2 reads the same row (tracks with original xmin)
        using var scope2 = _provider.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        var row2 = await db2.DailySummaries
            .FirstAsync(s => s.MerchantId == strongMerchantId && s.Date == date);

        // Context 1 modifies and saves — xmin changes in the database
        summary.ApplyTransaction(TransactionType.Credit, new Money(50m));
        await db1.SaveChangesAsync();

        // Context 2 tries to save with stale xmin — must throw
        row2.ApplyTransaction(TransactionType.Debit, new Money(30m));
        var act = () => db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// Publishes 10 Credit transactions for the same merchant and date.
    /// The consumer definition's partitioner serializes by key, and the retry
    /// middleware handles any xmin conflicts. The final DailySummary must reflect
    /// all 10 transactions without lost updates.
    /// This test uses real PostgreSQL (not InMemory) to exercise xmin row versioning.
    /// </summary>
    [Fact]
    public async Task ConcurrentMessages_SameMerchantAndDate_ShouldConsolidateCorrectly()
    {
        var merchantId = Guid.NewGuid();
        var strongMerchantId = new MerchantId(merchantId);
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-20);
        const int messageCount = 10;
        const decimal amountPerMessage = 100.00m;

        // Publish all messages concurrently
        var publishTasks = Enumerable.Range(0, messageCount)
            .Select(_ => _harness.Bus.Publish<ITransactionCreated>(new
            {
                TransactionId = Guid.NewGuid(),
                MerchantId = merchantId,
                ReferenceDate = date,
                TransactionType = "Credit",
                Amount = amountPerMessage,
                Currency = "BRL"
            }));

        await Task.WhenAll(publishTasks);

        // Wait for all messages to be consumed (poll with timeout)
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var consumed = 0;
        while (DateTime.UtcNow < deadline)
        {
            consumed = _harness.Consumed.Select<ITransactionCreated>()
                .Count(x => x.Context.Message.MerchantId == merchantId);
            if (consumed >= messageCount)
                break;
            await Task.Delay(500);
        }

        consumed.Should().Be(messageCount,
            "all messages should be consumed within timeout");

        // Assert final state
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        var result = await db.DailySummaries
            .FirstOrDefaultAsync(s => s.MerchantId == strongMerchantId && s.Date == date);

        result.Should().NotBeNull();
        result!.TransactionCount.Should().Be(messageCount);
        result.TotalCredits.Amount.Should().Be(messageCount * amountPerMessage);
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
