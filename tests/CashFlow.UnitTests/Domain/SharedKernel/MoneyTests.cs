using CashFlow.Domain.SharedKernel;
using FluentAssertions;

namespace CashFlow.UnitTests.Domain.SharedKernel;

public class MoneyTests
{
    [Fact]
    public void Zero_ShouldReturnMoneyWithZeroAmount()
    {
        var zero = Money.Zero;
        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Add_SameCurrency_ShouldSumAmounts()
    {
        var a = new Money(100m);
        var b = new Money(50m);
        var result = a + b;
        result.Amount.Should().Be(150m);
    }

    [Fact]
    public void Subtract_SameCurrency_ShouldSubtractAmounts()
    {
        var a = new Money(100m);
        var b = new Money(30m);
        var result = a - b;
        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void Add_DifferentCurrencies_ShouldThrow()
    {
        var brl = new Money(100m, "BRL");
        var usd = new Money(50m, "USD");
        var act = () => brl + usd;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_DifferentCurrencies_ShouldThrow()
    {
        var brl = new Money(100m, "BRL");
        var usd = new Money(50m, "USD");
        var act = () => brl - usd;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }
}
