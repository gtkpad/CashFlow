namespace CashFlow.Domain.SharedKernel;

public record Money(decimal Amount, string Currency = "BRL")
{
    public static Money Zero => new(0m);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException(
                $"Cannot add different currencies: {a.Currency} and {b.Currency}");
        return a with { Amount = a.Amount + b.Amount };
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException(
                $"Cannot subtract different currencies: {a.Currency} and {b.Currency}");
        return a with { Amount = a.Amount - b.Amount };
    }
}