namespace CashFlow.Domain.Transactions;

public readonly record struct TransactionId
{
    public Guid Value { get; }

    public TransactionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("TransactionId cannot be empty.", nameof(value));
        Value = value;
    }

    public static TransactionId New() => new(Guid.NewGuid());
}
