namespace CashFlow.Domain.SharedKernel;

public sealed class CurrencyMismatchException : InvalidOperationException
{
    public string CurrencyA { get; }
    public string CurrencyB { get; }

    public CurrencyMismatchException(string currencyA, string currencyB)
        : base($"Cannot perform operation on different currencies: {currencyA} and {currencyB}")
    {
        CurrencyA = currencyA;
        CurrencyB = currencyB;
    }

    public CurrencyMismatchException() : base()
    {
        CurrencyA = string.Empty;
        CurrencyB = string.Empty;
    }

    public CurrencyMismatchException(string message) : base(message)
    {
        CurrencyA = string.Empty;
        CurrencyB = string.Empty;
    }

    public CurrencyMismatchException(string message, Exception innerException) : base(message, innerException)
    {
        CurrencyA = string.Empty;
        CurrencyB = string.Empty;
    }
}
