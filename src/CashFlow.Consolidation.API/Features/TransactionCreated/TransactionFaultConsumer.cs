using CashFlow.Domain.IntegrationEvents;
using CashFlow.ServiceDefaults;
using MassTransit;

namespace CashFlow.Consolidation.API.Features.TransactionCreated;

public class TransactionFaultConsumer(
    CashFlowMetrics metrics,
    ILogger<TransactionFaultConsumer> logger) : IConsumer<Fault<ITransactionCreated>>
{
    public Task Consume(ConsumeContext<Fault<ITransactionCreated>> context)
    {
        var faulted = context.Message;
        var exceptionType = faulted.Exceptions.FirstOrDefault()?.ExceptionType ?? "Unknown";

        logger.LogError(
            "Transaction moved to error queue: MessageId={MessageId}, ExceptionType={ExceptionType}, Message={Message}",
            faulted.FaultedMessageId,
            exceptionType,
            faulted.Exceptions.FirstOrDefault()?.Message);

        metrics.RecordDlqFault(nameof(ITransactionCreated), exceptionType);

        return Task.CompletedTask;
    }
}
