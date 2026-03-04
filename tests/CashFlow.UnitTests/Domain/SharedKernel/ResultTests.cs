using CashFlow.Domain.SharedKernel;
using FluentAssertions;

namespace CashFlow.UnitTests.Domain.SharedKernel;

public class ResultTests
{
    [Fact]
    public void Success_ShouldHaveValue()
    {
        var result = Result.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_ShouldHaveError()
    {
        var result = Result.Failure<int>("Something went wrong");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void Failure_AccessingValue_ShouldThrow()
    {
        var result = Result.Failure<int>("error");
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
