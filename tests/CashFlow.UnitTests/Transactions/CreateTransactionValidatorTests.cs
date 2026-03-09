using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Features.CreateTransaction;
using FluentValidation.TestHelper;

namespace CashFlow.UnitTests.Transactions;

public class CreateTransactionValidatorTests
{
    private readonly CreateTransactionValidator _validator = new();

    private static CreateTransactionCommand ValidCommand() => new(
        DateOnly.FromDateTime(DateTime.Today),
        TransactionType.Credit,
        100.00m,
        "BRL",
        "Valid transaction",
        "user@test.com");

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ZeroAmount_ShouldFail()
    {
        var command = ValidCommand() with { Amount = 0m };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_NegativeAmount_ShouldFail()
    {
        var command = ValidCommand() with { Amount = -10m };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_EmptyCurrency_ShouldFail()
    {
        var command = ValidCommand() with { Currency = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_CurrencyNot3Chars_ShouldFail()
    {
        var command = ValidCommand() with { Currency = "BR" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_EmptyDescription_ShouldFail()
    {
        var command = ValidCommand() with { Description = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_DescriptionTooLong_ShouldFail()
    {
        var command = ValidCommand() with { Description = new string('x', 501) };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_InvalidTransactionType_ShouldFail()
    {
        var command = ValidCommand() with { Type = (TransactionType)99 };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Type);
    }
}
