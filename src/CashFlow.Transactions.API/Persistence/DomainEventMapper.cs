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

    private static object MapTransactionCreated(TransactionCreated e) => new
    {
        TransactionId = e.TransactionId.Value,
        MerchantId = e.MerchantId.Value,
        ReferenceDate = e.ReferenceDate,
        TransactionType = e.Type.ToString(),
        Amount = e.Value.Amount,
        Currency = e.Value.Currency
    };

    internal static Type? GetIntegrationEventType(IDomainEvent domainEvent) => domainEvent switch
    {
        TransactionCreated => typeof(ITransactionCreated),
        _ => null
    };
}
