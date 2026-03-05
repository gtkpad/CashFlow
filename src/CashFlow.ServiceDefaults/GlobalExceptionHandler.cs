using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CashFlow.ServiceDefaults;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapException(exception);

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning(exception, "Client error: {Message}", exception.Message);

        var detail = statusCode >= 500 && !environment.IsDevelopment()
            ? "An unexpected error occurred. Please try again later."
            : exception.Message;

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}"
        }, cancellationToken);

        return true;
    }

    public static (int StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest, "Argument Out of Range"),
        ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
        InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
        _ when IsDbConcurrencyException(exception) => (StatusCodes.Status409Conflict, "Concurrency Conflict"),
        _ when IsDbDuplicateKeyException(exception) => (StatusCodes.Status409Conflict, "Duplicate Resource"),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
    };

    private static bool IsDbConcurrencyException(Exception exception) =>
        exception.GetType().Name == "DbUpdateConcurrencyException";

    private static bool IsDbDuplicateKeyException(Exception exception) =>
        exception.GetType().Name == "DbUpdateException"
        && exception.InnerException?.Message is { } msg
        && (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase));
}
