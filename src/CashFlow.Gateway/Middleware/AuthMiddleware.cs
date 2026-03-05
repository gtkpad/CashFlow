using CashFlow.ServiceDefaults;

namespace CashFlow.Gateway.Middleware;

public class AuthMiddleware(RequestDelegate next, IConfiguration configuration,
    ILogger<AuthMiddleware> logger, CashFlowMetrics metrics)
{
    private static readonly string[] _publicPaths = ["/api/identity/"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            var hasToken = context.Request.Headers.ContainsKey("Authorization");
            var reason = hasToken ? "unauthorized" : "missing_token";
            logger.LogWarning("Unauthorized request to {Path} from {RemoteIp}, reason={Reason}",
                path, context.Connection.RemoteIpAddress, reason);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            metrics.RecordAuthFailure(reason);
            return;
        }

        var userId = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId is not null)
            context.Request.Headers["X-User-Id"] = userId;

        var gatewaySecret = configuration["Gateway:Secret"];
        if (!string.IsNullOrEmpty(gatewaySecret))
            context.Request.Headers["X-Gateway-Secret"] = gatewaySecret;

        await next(context);
    }
}
