using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Transactions;

public record TransactionCreated : IDomainEvent
{
    public TransactionId TransactionId { get; init; }
    public Guid MerchantId { get; init; }
    public DateOnly ReferenceDate { get; init; }
    public TransactionType Type { get; init; }
    public Money Value { get; init; } = Money.Zero;
}