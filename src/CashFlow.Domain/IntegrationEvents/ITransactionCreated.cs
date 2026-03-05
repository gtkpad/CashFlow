namespace CashFlow.Domain.IntegrationEvents;

public interface ITransactionCreated
{
    Guid TransactionId { get; }
    Guid MerchantId { get; }
    DateOnly ReferenceDate { get; }
    string TransactionType { get; }
    decimal Amount { get; }
    string Currency { get; }
}
