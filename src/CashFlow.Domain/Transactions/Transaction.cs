using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Transactions;

public sealed class Transaction : Entity<TransactionId>
{
    public Guid MerchantId { get; private init; }
    public DateOnly ReferenceDate { get; private init; }
    public TransactionType Type { get; private init; }
    public Money Value { get; private init; } = Money.Zero;
    public string Description { get; private init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private init; }
    public string? CreatedBy { get; private init; }

    private Transaction() { }

    public static Result<Transaction> Create(
        Guid merchantId, DateOnly date, TransactionType type, Money value,
        string description, string? user)
    {
        if (merchantId == Guid.Empty)
            return Result.Failure<Transaction>("MerchantId is required");
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
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user
        };

        transaction.AddDomainEvent(
            new TransactionCreated
            {
                TransactionId = transaction.Id,
                MerchantId = merchantId,
                ReferenceDate = date,
                Type = type,
                Value = value
            });

        return Result.Success(transaction);
    }
}