namespace CashFlow.Domain.SharedKernel;

public readonly record struct MerchantId
{
    public Guid Value { get; }

    public MerchantId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("MerchantId cannot be empty.", nameof(value));
        Value = value;
    }

    public static MerchantId New() => new(Guid.NewGuid());
}
