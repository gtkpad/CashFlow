using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Transactions;

public sealed record TransactionCreated : DomainEvent
{
    public required TransactionId TransactionId { get; init; }
    public required MerchantId MerchantId { get; init; }
    public required DateOnly ReferenceDate { get; init; }
    public required TransactionType Type { get; init; }
    public required Money Value { get; init; }
}
