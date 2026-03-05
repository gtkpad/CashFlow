using CashFlow.ServiceDefaults;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CashFlow.UnitTests.ServiceDefaults;

public class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(ArgumentOutOfRangeException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status409Conflict)]
    public void MapException_ShouldReturnCorrectStatusCode(Type exceptionType, int expectedStatusCode)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test message")!;

        var (statusCode, _) = GlobalExceptionHandler.MapException(exception);

        statusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public void MapException_ArgumentOutOfRange_ShouldBe400_NotConfusedWithArgument()
    {
        // ArgumentOutOfRangeException inherits from ArgumentException;
        // the handler must check it first to avoid falling into the generic 400
        var exception = new ArgumentOutOfRangeException("param", "out of range");

        var (statusCode, title) = GlobalExceptionHandler.MapException(exception);

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        title.Should().Be("Argument Out of Range");
    }

    [Fact]
    public void MapException_UnknownException_ShouldReturn500()
    {
        var exception = new TimeoutException("timed out");

        var (statusCode, _) = GlobalExceptionHandler.MapException(exception);

        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
