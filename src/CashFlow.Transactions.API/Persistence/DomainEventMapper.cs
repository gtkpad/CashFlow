using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using MassTransit;

namespace CashFlow.Transactions.API.Persistence;

public static class DomainEventMapper
{
    public static async Task PublishIntegrationEvent(
        IDomainEvent domainEvent,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case TransactionCreated e:
                await publishEndpoint.Publish<ITransactionCreated>(
                    MapTransactionCreated(e), cancellationToken);
                break;
        }
    }

    private record TransactionCreatedEvent(
        Guid TransactionId, Guid MerchantId, DateOnly ReferenceDate,
        string TransactionType, decimal Amount, string Currency) : ITransactionCreated;

    private static ITransactionCreated MapTransactionCreated(TransactionCreated e) =>
        new TransactionCreatedEvent(
            e.TransactionId.Value, e.MerchantId.Value, e.ReferenceDate,
            e.Type.ToString(), e.Value.Amount, e.Value.Currency);
}
