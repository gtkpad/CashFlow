using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CashFlow.IntegrationTests.Consolidation;

public class TransactionCreatedConsumerTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        var dbName = $"consolidation-test-{Guid.NewGuid()}";
        services.AddDbContext<ConsolidationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));

        services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        services.AddSingleton(Substitute.For<IOutputCacheStore>());
        services.AddLogging();

        // Register consumer WITHOUT definition to avoid Outbox middleware
        // (InMemory DB doesn't support the transaction scope required by EF Outbox)
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<TransactionCreatedConsumer>();
        });

        _provider = services.BuildServiceProvider();
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    [Fact]
    public async Task Should_Consume_And_Create_DailySummary()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var merchantStrongId = new MerchantId(merchantId);
        var date = DateOnly.FromDateTime(DateTime.Today);

        // Act
        await _harness.Bus.Publish<ITransactionCreated>(new
        {
            TransactionId = Guid.NewGuid(),
            MerchantId = merchantId,
            ReferenceDate = date,
            TransactionType = "Credit",
            Amount = 100.00m,
            Currency = "BRL"
        });

        // Assert
        (await _harness.Consumed.Any<ITransactionCreated>(x =>
            x.Context.Message.MerchantId == merchantId)).Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        var summary = await db.DailySummaries
            .FirstOrDefaultAsync(s => s.MerchantId == merchantStrongId && s.Date == date);

        summary.Should().NotBeNull();
        summary!.TransactionCount.Should().Be(1);
        summary.TotalCredits.Amount.Should().Be(100.00m);
    }

    [Fact]
    public async Task Should_Consolidate_Multiple_Transactions()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var merchantStrongId = new MerchantId(merchantId);
        var date = DateOnly.FromDateTime(DateTime.Today);

        // Act - publish two transactions sequentially
        await _harness.Bus.Publish<ITransactionCreated>(new
        {
            TransactionId = Guid.NewGuid(),
            MerchantId = merchantId,
            ReferenceDate = date,
            TransactionType = "Credit",
            Amount = 200.00m,
            Currency = "BRL"
        });

        // Wait for first to be consumed before publishing second
        await _harness.Consumed.Any<ITransactionCreated>(x =>
            x.Context.Message.MerchantId == merchantId);

        await _harness.Bus.Publish<ITransactionCreated>(new
        {
            TransactionId = Guid.NewGuid(),
            MerchantId = merchantId,
            ReferenceDate = date,
            TransactionType = "Debit",
            Amount = 50.00m,
            Currency = "BRL"
        });

        // Wait for second message to be consumed
        (await _harness.Consumed.Any<ITransactionCreated>(x =>
            x.Context.Message.MerchantId == merchantId && x.Context.Message.Amount == 50.00m))
            .Should().BeTrue();

        // Assert
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        var summary = await db.DailySummaries
            .FirstOrDefaultAsync(s => s.MerchantId == merchantStrongId && s.Date == date);

        summary.Should().NotBeNull();
        summary!.TransactionCount.Should().Be(2);
        summary.TotalCredits.Amount.Should().Be(200.00m);
        summary.TotalDebits.Amount.Should().Be(50.00m);
        summary.Balance.Amount.Should().Be(150.00m);
    }

    [Fact]
    public async Task Should_Not_Produce_Faults_For_Valid_Messages()
    {
        // Act
        await _harness.Bus.Publish<ITransactionCreated>(new
        {
            TransactionId = Guid.NewGuid(),
            MerchantId = Guid.NewGuid(),
            ReferenceDate = DateOnly.FromDateTime(DateTime.Today),
            TransactionType = "Debit",
            Amount = 50.00m,
            Currency = "BRL"
        });

        // Assert — wait for consumption, then verify no faults (bounded wait)
        (await _harness.Consumed.Any<ITransactionCreated>()).Should().BeTrue();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        (await _harness.Published.Any<Fault<ITransactionCreated>>(cts.Token)).Should().BeFalse();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }
}
