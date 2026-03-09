using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CashFlow.ServiceDefaults;

public sealed class MerchantIdFilter : IEndpointFilter
{
    internal const string MerchantIdKey = "MerchantId";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader)
            || !Guid.TryParse(userIdHeader, out var merchantId))
        {
            return Results.Problem(
                "Missing or invalid X-User-Id header.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        httpContext.Items[MerchantIdKey] = merchantId;
        return await next(context);
    }
}

public static class MerchantIdExtensions
{
    public static RouteHandlerBuilder RequireMerchantId(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<MerchantIdFilter>();

    public static RouteGroupBuilder RequireMerchantId(this RouteGroupBuilder builder)
        => builder.AddEndpointFilter<MerchantIdFilter>();

    public static Guid GetMerchantId(this HttpContext httpContext)
    {
        if (httpContext.Items[MerchantIdFilter.MerchantIdKey] is not Guid merchantId)
        {
            throw new InvalidOperationException(
                "MerchantId not found in HttpContext.Items. " +
                "Ensure the endpoint group is decorated with RequireMerchantId().");
        }

        return merchantId;
    }
}
