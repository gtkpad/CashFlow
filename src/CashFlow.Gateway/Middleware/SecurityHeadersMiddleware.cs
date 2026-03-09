using System.Diagnostics;

namespace CashFlow.Gateway.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=()";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        var traceId = Activity.Current?.TraceId.ToString();
        if (traceId is not null)
            headers["X-Trace-Id"] = traceId;

        await next(context);
    }
}
