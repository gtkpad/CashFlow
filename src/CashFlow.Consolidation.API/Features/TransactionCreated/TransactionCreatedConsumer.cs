using System.Diagnostics;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.ServiceDefaults;
using MassTransit;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Features.TransactionCreated;

public class TransactionCreatedConsumer(
    IDailySummaryRepository repo,
    ConsolidationDbContext db,
    IOutputCacheStore cacheStore,
    ILogger<TransactionCreatedConsumer> logger,
    CashFlowMetrics metrics) : IConsumer<ITransactionCreated>
{
    public async Task Consume(ConsumeContext<ITransactionCreated> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var evt = context.Message;
        var merchantId = new MerchantId(evt.MerchantId);
        var type = Enum.Parse<TransactionType>(evt.TransactionType);

        logger.LogInformation(
            "Processing TransactionCreated: MerchantId={MerchantId}, Date={Date}, Type={Type}, Amount={Amount}",
            evt.MerchantId, evt.ReferenceDate, evt.TransactionType, evt.Amount);

        var summary = await repo.GetByDateAndMerchant(merchantId, evt.ReferenceDate)
                      ?? DailySummary.CreateForDay(merchantId, evt.ReferenceDate);

        summary.ApplyTransaction(type, new Money(evt.Amount, evt.Currency));

        if (summary.TransactionCount == 1)
            await repo.AddAsync(summary);
        else
            await repo.Save(summary);

        await db.SaveChangesAsync();

        var tag = $"balance-{evt.MerchantId}-{evt.ReferenceDate:yyyy-MM-dd}";
        await cacheStore.EvictByTagAsync(tag, context.CancellationToken);

        stopwatch.Stop();
        metrics.RecordConsolidationEventProcessed("success");
        metrics.RecordConsolidationProcessingDuration(stopwatch.Elapsed.TotalMilliseconds);

        if (context.SentTime.HasValue)
        {
            var consistencyMs = (DateTimeOffset.UtcNow - context.SentTime.Value).TotalMilliseconds;
            metrics.RecordEventualConsistency(consistencyMs);
        }

        logger.LogInformation(
            "Consolidated successfully: MerchantId={MerchantId}, Date={Date}, Balance={Balance}",
            evt.MerchantId, evt.ReferenceDate, summary.Balance.Amount);
    }
}

public class TransactionCreatedConsumerDefinition
    : ConsumerDefinition<TransactionCreatedConsumer>
{
    public TransactionCreatedConsumerDefinition()
    {
        EndpointName = "consolidation-transaction-created";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TransactionCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // MIDDLEWARE ORDER IS CRITICAL (LIFO pipeline):
        // Outermost -> Innermost: CircuitBreaker -> Redelivery -> Retry -> Outbox
        var partition = endpointConfigurator.CreatePartitioner(8);

        consumerConfigurator.Message<ITransactionCreated>(
            m => m.UsePartitioner(partition,
                x => $"{x.Message.MerchantId}:{x.Message.ReferenceDate:yyyy-MM-dd}"));

        endpointConfigurator.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });

        endpointConfigurator.UseDelayedRedelivery(r =>
            r.Intervals(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromMinutes(60)));

        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Exponential(5,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMilliseconds(50));
            r.Handle<DbUpdateConcurrencyException>();
            r.Ignore<ArgumentException>();
        });

        endpointConfigurator.UseEntityFrameworkOutbox<ConsolidationDbContext>(context);
    }
}
