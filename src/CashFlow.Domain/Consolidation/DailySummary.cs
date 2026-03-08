using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Consolidation;

public sealed class DailySummary : Entity<DailySummaryId>, IAggregateRoot
{
    public MerchantId MerchantId { get; private init; }
    public DateOnly Date { get; private init; }
    public Money TotalCredits { get; private set; } = Money.Zero;
    public Money TotalDebits { get; private set; } = Money.Zero;
    public decimal Balance => TotalCredits.Amount - TotalDebits.Amount;
    public int TransactionCount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DailySummary() { }

    /// <summary>
    /// Applies a transaction to this daily summary. Throws on invalid input because this
    /// method is called exclusively from the MassTransit consumer, which provides retry
    /// and dead-letter queue semantics — exceptions here trigger automatic retries rather
    /// than being returned as API responses.
    /// </summary>
    public void ApplyTransaction(TransactionType type, Money value, TimeProvider? clock = null)
    {
        if (value.Amount <= 0)
            throw new ArgumentException("Transaction value must be positive.", nameof(value));

        switch (type)
        {
            case TransactionType.Credit: TotalCredits += value; break;
            case TransactionType.Debit: TotalDebits += value; break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Invalid transaction type.");
        }

        TransactionCount++;
        UpdatedAt = (clock ?? TimeProvider.System).GetUtcNow();
    }

    public static DailySummary CreateForDay(MerchantId merchantId, DateOnly date, TimeProvider? clock = null) => new()
    {
        Id = DailySummaryId.New(),
        MerchantId = merchantId,
        Date = date,
        TotalCredits = Money.Zero,
        TotalDebits = Money.Zero,
        TransactionCount = 0,
        UpdatedAt = (clock ?? TimeProvider.System).GetUtcNow()
    };
}
