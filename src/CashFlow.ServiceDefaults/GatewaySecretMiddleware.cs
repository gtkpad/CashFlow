using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CashFlow.ServiceDefaults;

public class GatewaySecretMiddleware(RequestDelegate next, IConfiguration configuration)
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
            await next(context);
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
