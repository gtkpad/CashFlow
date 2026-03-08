using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using MassTransit;

namespace CashFlow.Transactions.API.Persistence;

public static class DomainEventMapper
{
    private static readonly Dictionary<Type, Func<IDomainEvent, IPublishEndpoint, CancellationToken, Task>>
        Publishers = new()
        {
            [typeof(TransactionCreated)] = (e, pub, ct) =>
                pub.Publish<ITransactionCreated>(MapTransactionCreated((TransactionCreated)e), ct)
        };

    public static Task PublishIntegrationEvent(
        IDomainEvent domainEvent,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        return Publishers.TryGetValue(domainEvent.GetType(), out var publisher)
            ? publisher(domainEvent, publishEndpoint, cancellationToken)
            : Task.CompletedTask;
    }

    private record TransactionCreatedEvent(
        Guid TransactionId, Guid MerchantId, DateOnly ReferenceDate,
        string TransactionType, decimal Amount, string Currency) : ITransactionCreated;

    private static ITransactionCreated MapTransactionCreated(TransactionCreated e) =>
        new TransactionCreatedEvent(
            e.TransactionId.Value, e.MerchantId.Value, e.ReferenceDate,
            e.Type.ToString(), e.Value.Amount, e.Value.Currency);
}
