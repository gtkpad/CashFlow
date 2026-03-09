namespace CashFlow.Domain.SharedKernel;

public sealed record Money : IValueObject
{
    /// <summary>
    ///     Creates a Money value object. Throws on invalid input because Value Objects
    ///     follow the always-valid pattern (DDD guard clauses). Invalid construction is
    ///     a programming error, not a user input error — the API boundary (GlobalExceptionHandler)
    ///     translates these into HTTP 400 responses.
    /// </summary>
    public Money(decimal amount, string currency = "BRL")
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }
    public string Currency { get; }

    public static Money Zero => new(0m);

    public bool IsPositive() => Amount > 0;
    public bool IsZero() => Amount == 0;

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new CurrencyMismatchException(a.Currency, b.Currency);
    }
}
