using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CashFlow.ServiceDefaults;

public class GatewaySecretMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<GatewaySecretMiddleware> logger)
{
    private static readonly string[] _bypassPaths = ["/health", "/alive"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (_bypassPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var expectedSecret = configuration["Gateway:Secret"];
        if (string.IsNullOrEmpty(expectedSecret))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning("Gateway:Secret is not configured. Allowing request in Development mode");
                await next(context);
                return;
            }

            logger.LogCritical("Gateway:Secret is not configured. Blocking all requests in non-Development mode");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var providedSecret = context.Request.Headers["X-Gateway-Secret"].ToString();
        if (!string.Equals(expectedSecret, providedSecret, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
