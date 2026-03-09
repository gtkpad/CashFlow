namespace CashFlow.Domain.Transactions;

public readonly record struct TransactionId
{
    public TransactionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("TransactionId cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }

    public static TransactionId New() => new(Guid.NewGuid());
}
