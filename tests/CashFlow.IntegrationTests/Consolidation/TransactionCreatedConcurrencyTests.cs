using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using CashFlow.ServiceDefaults;
using Testcontainers.PostgreSql;

namespace CashFlow.IntegrationTests.Consolidation;

// Same collection as all other integration tests — see TransactionCreatedConsumerTests for rationale.
[Collection("IntegrationTests")]
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

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();

        services.AddDbContext<ConsolidationDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString()));

        services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        services.AddScoped<TransactionCreatedConsumer>();
        services.AddSingleton(Substitute.For<IOutputCacheStore>());
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<CashFlowMetrics>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        await db.Database.MigrateAsync();
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
    /// Invokes the consumer directly for 10 sequential Credit transactions for the same
    /// merchant and date. Bypassing the MassTransit test harness avoids LoopbackTransport
    /// shared-state issues (the in-process singleton routing table is not cleaned up between
    /// test harness instances). The final DailySummary must reflect all 10 transactions.
    /// </summary>
    [Fact]
    public async Task ConcurrentMessages_SameMerchantAndDate_ShouldConsolidateCorrectly()
    {
        var merchantId = Guid.NewGuid();
        var strongMerchantId = new MerchantId(merchantId);
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-20);
        const int messageCount = 10;
        const decimal amountPerMessage = 100.00m;

        for (var i = 0; i < messageCount; i++)
        {
            using var scope = _provider.CreateScope();
            var consumer = scope.ServiceProvider.GetRequiredService<TransactionCreatedConsumer>();
            var context = BuildConsumeContext(Guid.NewGuid(), merchantId, date, "Credit", amountPerMessage);
            await consumer.Consume(context);
        }

        using var assertScope = _provider.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        var result = await db.DailySummaries
            .FirstOrDefaultAsync(s => s.MerchantId == strongMerchantId && s.Date == date);

        result.Should().NotBeNull();
        result!.TransactionCount.Should().Be(messageCount);
        result.TotalCredits.Amount.Should().Be(messageCount * amountPerMessage);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static ConsumeContext<ITransactionCreated> BuildConsumeContext(
        Guid txId, Guid merchantId, DateOnly date, string type, decimal amount)
    {
        var message = Substitute.For<ITransactionCreated>();
        message.TransactionId.Returns(txId);
        message.MerchantId.Returns(merchantId);
        message.ReferenceDate.Returns(date);
        message.TransactionType.Returns(type);
        message.Amount.Returns(amount);
        message.Currency.Returns("BRL");

        var context = Substitute.For<ConsumeContext<ITransactionCreated>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        context.SentTime.Returns((DateTime?)null);
        return context;
    }
}
