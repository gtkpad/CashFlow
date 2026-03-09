using System.Diagnostics.Metrics;
using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.ServiceDefaults;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CashFlow.UnitTests.Consolidation;

public class TransactionCreatedConsumerTests : IDisposable
{
    private readonly IOutputCacheStore _cacheStore;
    private readonly TransactionCreatedConsumer _consumer;
    private readonly ConsolidationDbContext _db;
    private readonly CashFlowMetrics _metrics;
    private readonly IDailySummaryRepository _repo;

    public TransactionCreatedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<ConsolidationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ConsolidationDbContext(options);
        _repo = new DailySummaryRepository(_db);
        _cacheStore = Substitute.For<IOutputCacheStore>();
        var meterFactory = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();
        _metrics = new CashFlowMetrics(meterFactory);

        _consumer = new TransactionCreatedConsumer(
            _repo, _db, _cacheStore,
            NullLogger<TransactionCreatedConsumer>.Instance, _metrics);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Consume_ZeroAmount_ThrowsArgumentException()
    {
        var message = CreateMessage(amount: 0m);
        var context = CreateConsumeContext(message);

        var act = () => _consumer.Consume(context);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*positive*");
    }

    [Fact]
    public async Task Consume_InvalidTransactionType_ThrowsInvalidOperationException()
    {
        var message = Substitute.For<ITransactionCreated>();
        message.MerchantId.Returns(Guid.NewGuid());
        message.ReferenceDate.Returns(new DateOnly(2025, 6, 15));
        message.TransactionType.Returns("InvalidType");
        message.Amount.Returns(100m);
        message.Currency.Returns("BRL");

        var context = CreateConsumeContext(message);

        var act = () => _consumer.Consume(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown TransactionType*");
    }

    [Fact]
    public async Task Consume_MultipleEventsForSameDay_AccumulatesBalance()
    {
        var merchantId = Guid.NewGuid();
        var date = new DateOnly(2025, 6, 15);

        var credit = CreateMessage(merchantId, date, 200m, "Credit");
        await _consumer.Consume(CreateConsumeContext(credit));

        var debit = CreateMessage(merchantId, date, 50m, "Debit");
        await _consumer.Consume(CreateConsumeContext(debit));

        var summary = await _db.DailySummaries
            .FirstOrDefaultAsync(d => d.MerchantId == new MerchantId(merchantId) && d.Date == date);
        summary.Should().NotBeNull();
        summary!.Balance.Should().Be(150m);
        summary.TransactionCount.Should().Be(2);
    }

    [Fact]
    public async Task Consume_EvictsCacheAfterProcessing()
    {
        var message = CreateMessage();
        var context = CreateConsumeContext(message);

        await _consumer.Consume(context);

        await _cacheStore.Received(1).EvictByTagAsync(
            Arg.Is<string>(tag => tag.StartsWith("balance-")),
            Arg.Any<CancellationToken>());
    }

    private static ITransactionCreated CreateMessage(
        Guid? merchantId = null,
        DateOnly? date = null,
        decimal amount = 100m,
        string type = "Credit",
        string currency = "BRL")
    {
        var message = Substitute.For<ITransactionCreated>();
        message.MerchantId.Returns(merchantId ?? Guid.NewGuid());
        message.ReferenceDate.Returns(date ?? new DateOnly(2025, 6, 15));
        message.TransactionType.Returns(type);
        message.Amount.Returns(amount);
        message.Currency.Returns(currency);
        return message;
    }

    private static ConsumeContext<ITransactionCreated> CreateConsumeContext(ITransactionCreated message)
    {
        var context = Substitute.For<ConsumeContext<ITransactionCreated>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        context.SentTime.Returns((DateTime?)null);
        return context;
    }
}
