namespace CashFlow.Domain.Consolidation;

public readonly record struct DailySummaryId
{
    public Guid Value { get; }

    public DailySummaryId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DailySummaryId cannot be empty.", nameof(value));
        Value = value;
    }

    public static DailySummaryId New() => new(Guid.NewGuid());
}
