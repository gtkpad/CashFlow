using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using FluentAssertions;

namespace CashFlow.UnitTests.Domain.Consolidation;

public class DailySummaryTests
{
    private readonly MerchantId _merchantId = MerchantId.New();
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public void CreateForDay_ShouldInitializeWithZeros()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        summary.MerchantId.Should().Be(_merchantId);
        summary.Date.Should().Be(_today);
        summary.TotalCredits.Amount.Should().Be(0m);
        summary.TotalDebits.Amount.Should().Be(0m);
        summary.Balance.Should().Be(0m);
        summary.TransactionCount.Should().Be(0);
    }

    [Fact]
    public void ApplyTransaction_Credit_ShouldIncreaseTotalCredits()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        summary.ApplyTransaction(TransactionType.Credit, new Money(100m));

        summary.TotalCredits.Amount.Should().Be(100m);
        summary.TotalDebits.Amount.Should().Be(0m);
        summary.Balance.Should().Be(100m);
        summary.TransactionCount.Should().Be(1);
    }

    [Fact]
    public void ApplyTransaction_Debit_ShouldIncreaseTotalDebits()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        summary.ApplyTransaction(TransactionType.Debit, new Money(50m));

        summary.TotalCredits.Amount.Should().Be(0m);
        summary.TotalDebits.Amount.Should().Be(50m);
        summary.Balance.Should().Be(-50m);
        summary.TransactionCount.Should().Be(1);
    }

    [Fact]
    public void ApplyTransaction_MultipleTransactions_ShouldAccumulate()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        summary.ApplyTransaction(TransactionType.Credit, new Money(200m));
        summary.ApplyTransaction(TransactionType.Debit, new Money(80m));
        summary.ApplyTransaction(TransactionType.Credit, new Money(50m));

        summary.TotalCredits.Amount.Should().Be(250m);
        summary.TotalDebits.Amount.Should().Be(80m);
        summary.Balance.Should().Be(170m);
        summary.TransactionCount.Should().Be(3);
    }

    [Fact]
    public void ApplyTransaction_ZeroValue_ShouldThrow()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        var act = () => summary.ApplyTransaction(TransactionType.Credit, new Money(0m));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*positive*");
    }

    [Fact]
    public void ApplyTransaction_NegativeValue_ShouldThrow()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        var act = () => summary.ApplyTransaction(TransactionType.Credit, new Money(-10m));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void ApplyTransaction_DifferentCurrency_ShouldThrow()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);
        summary.ApplyTransaction(TransactionType.Credit, new Money(100m));

        var act = () => summary.ApplyTransaction(TransactionType.Credit, new Money(50m, "USD"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void ApplyTransaction_InvalidType_ShouldThrow()
    {
        var summary = DailySummary.CreateForDay(_merchantId, _today);

        var act = () => summary.ApplyTransaction((TransactionType)99, new Money(10m));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
