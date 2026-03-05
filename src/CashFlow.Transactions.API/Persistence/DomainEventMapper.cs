using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;

namespace CashFlow.Transactions.API.Persistence;

public static class DomainEventMapper
{
    public static object? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        TransactionCreated e => MapTransactionCreated(e),
        _ => null
    };

    private record TransactionCreatedEvent(
        Guid TransactionId, Guid MerchantId, DateOnly ReferenceDate,
        string TransactionType, decimal Amount, string Currency) : ITransactionCreated;

    private static ITransactionCreated MapTransactionCreated(TransactionCreated e) =>
        new TransactionCreatedEvent(
            e.TransactionId.Value, e.MerchantId.Value, e.ReferenceDate,
            e.Type.ToString(), e.Value.Amount, e.Value.Currency);

    internal static Type? GetIntegrationEventType(IDomainEvent domainEvent) => domainEvent switch
    {
        TransactionCreated => typeof(ITransactionCreated),
        _ => null
    };
}
