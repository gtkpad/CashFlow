using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Transactions;

public sealed class Transaction : Entity<TransactionId>, IAggregateRoot
{
    private Transaction()
    {
    }

    public MerchantId MerchantId { get; private init; }
    public DateOnly ReferenceDate { get; private init; }
    public TransactionType Type { get; private init; }
    public Money Value { get; private init; } = Money.Zero;
    public string Description { get; private init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private init; }
    public string? CreatedBy { get; private init; }

    /// <summary>
    ///     Factory method using Result&lt;T&gt; because it is called from API handlers where
    ///     validation errors must be composed and returned as structured responses (not exceptions).
    ///     Contrast with Value Object constructors (throw) and domain methods called from
    ///     MassTransit consumers (throw with retry semantics).
    /// </summary>
    public static Result<Transaction> Create(
        MerchantId merchantId, DateOnly date, TransactionType type, Money value,
        string description, string? user, TimeProvider? clock = null)
    {
        if (value.Amount <= 0)
            return Result.Failure<Transaction>("Value must be positive");
        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<Transaction>("Description is required");

        var transaction = new Transaction
        {
            Id = TransactionId.New(),
            MerchantId = merchantId,
            ReferenceDate = date,
            Type = type,
            Value = value,
            Description = description,
            CreatedAt = (clock ?? TimeProvider.System).GetUtcNow(),
            CreatedBy = user
        };

        transaction.Raise(
            new TransactionCreated
            {
                OccurredAt = (clock ?? TimeProvider.System).GetUtcNow(),
                TransactionId = transaction.Id,
                MerchantId = merchantId,
                ReferenceDate = date,
                Type = type,
                Value = value
            });

        return Result.Success(transaction);
    }
}
