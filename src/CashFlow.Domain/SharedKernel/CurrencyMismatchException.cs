namespace CashFlow.Domain.SharedKernel;

public sealed class CurrencyMismatchException(string currencyA, string currencyB)
    : InvalidOperationException(
        $"Cannot perform operation on different currencies: {currencyA} and {currencyB}")
{
    public string CurrencyA { get; } = currencyA;
    public string CurrencyB { get; } = currencyB;
}
