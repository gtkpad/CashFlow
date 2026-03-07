using Carter;
using CashFlow.ServiceDefaults;

namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public record GetDailyBalanceResponse(
    DateOnly Date, decimal TotalCredits, decimal TotalDebits,
    decimal Balance, int TransactionCount);

public sealed class GetDailyBalanceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/consolidation")
            .RequireMerchantId()
            .WithTags("Consolidation")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{date}", async (
            DateOnly date, HttpContext httpContext,
            GetDailyBalanceHandler handler, CancellationToken ct) =>
        {
            var merchantId = httpContext.GetMerchantId();
            var result = await handler.HandleAsync(merchantId, date, ct);
            return result is not null
                ? Results.Ok(result)
                : Results.Problem(
                    detail: $"No consolidated data found for date {date:yyyy-MM-dd}.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Resource Not Found");
        })
        .WithName("GetDailyBalance")
        .WithSummary("Get daily consolidated balance")
        .WithDescription("Returns the consolidated balance for a given date, including total credits, "
            + "debits, net balance, and transaction count.")
        .Produces<GetDailyBalanceResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .CacheOutput("DailyBalance");
    }
}
