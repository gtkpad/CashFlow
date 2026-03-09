using CashFlow.ServiceDefaults;

namespace CashFlow.Gateway.Middleware;

public sealed class AuthMiddleware(RequestDelegate next, IConfiguration configuration,
    ILogger<AuthMiddleware> logger, CashFlowMetrics metrics)
{
    private static readonly string[] _publicPaths = ["/api/identity/"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Inject gateway secret on all proxied requests for defense-in-depth
        // Remove first to prevent header spoofing by untrusted clients
        var gatewaySecret = configuration["Gateway:Secret"];
        if (!string.IsNullOrEmpty(gatewaySecret))
        {
            context.Request.Headers.Remove("X-Gateway-Secret");
            context.Request.Headers.Append("X-Gateway-Secret", gatewaySecret);
        }

        if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (path.StartsWith(WellKnownPaths.Health, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(WellKnownPaths.Alive, StringComparison.OrdinalIgnoreCase))
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

        if (userId is null)
        {
            logger.LogWarning("Authenticated user has no 'sub' claim on {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            metrics.RecordAuthFailure("missing_sub_claim");
            return;
        }

        context.Request.Headers.Remove("X-User-Id");
        context.Request.Headers.Append("X-User-Id", userId);

        await next(context);
    }
}
