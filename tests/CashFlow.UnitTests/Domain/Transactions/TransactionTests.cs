using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using FluentAssertions;

namespace CashFlow.UnitTests.Domain.Transactions;

public class TransactionTests
{
    private readonly MerchantId _merchantId = MerchantId.New();

    [Fact]
    public void Create_ValidInput_ShouldSucceed()
    {
        var result = Transaction.Create(
            _merchantId, DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(100m), "Sale #1", "user@test.com");

        result.IsSuccess.Should().BeTrue();
        result.Value.MerchantId.Should().Be(_merchantId);
        result.Value.Value.Amount.Should().Be(100m);
        result.Value.Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public void Create_ShouldEmitTransactionCreatedEvent()
    {
        var result = Transaction.Create(
            _merchantId, DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(100m), "Sale #1", "user@test.com");

        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TransactionCreated>();
    }

    [Fact]
    public void Create_EmptyMerchantId_ShouldThrow()
    {
        var act = () => Transaction.Create(
            new MerchantId(Guid.Empty), DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(100m), "Sale", "user");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*MerchantId*");
    }

    [Fact]
    public void Create_ZeroValue_ShouldFail()
    {
        var result = Transaction.Create(
            _merchantId, DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(0m), "Sale", "user");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Value");
    }

    [Fact]
    public void Create_NegativeValue_ShouldFail()
    {
        var result = Transaction.Create(
            _merchantId, DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Debit, new Money(-50m), "Expense", "user");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Value");
    }

    [Fact]
    public void Create_EmptyDescription_ShouldFail()
    {
        var result = Transaction.Create(
            _merchantId, DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(100m), "", "user");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Description");
    }
}
