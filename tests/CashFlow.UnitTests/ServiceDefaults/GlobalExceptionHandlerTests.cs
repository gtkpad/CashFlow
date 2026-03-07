using CashFlow.ServiceDefaults;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Npgsql;

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

    [Fact]
    public void MapException_DbUpdateConcurrencyException_ShouldReturn409()
    {
        var exception = new DbUpdateConcurrencyException("concurrency conflict");

        var (statusCode, title) = GlobalExceptionHandler.MapException(exception);

        statusCode.Should().Be(StatusCodes.Status409Conflict);
        title.Should().Be("Concurrency Conflict");
    }

    [Fact]
    public void MapException_DbUpdateExceptionWithDuplicateKey_ShouldReturn409()
    {
        var inner = new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505");
        var exception = new DbUpdateException("db error", inner);

        var (statusCode, title) = GlobalExceptionHandler.MapException(exception);

        statusCode.Should().Be(StatusCodes.Status409Conflict);
        title.Should().Be("Duplicate Resource");
    }

    [Fact]
    public void MapException_DbUpdateExceptionWithUniqueConstraint_ShouldReturn409()
    {
        var inner = new PostgresException("unique constraint violation on table X", "ERROR", "ERROR", "23505");
        var exception = new DbUpdateException("db error", inner);

        var (statusCode, title) = GlobalExceptionHandler.MapException(exception);

        statusCode.Should().Be(StatusCodes.Status409Conflict);
        title.Should().Be("Duplicate Resource");
    }

    // Nested classes whose GetType().Name matches what GlobalExceptionHandler checks
    private class DbUpdateConcurrencyException(string message) : Exception(message);
    private class DbUpdateException(string message, Exception inner) : Exception(message, inner);
}
