using Carter;
using CashFlow.ServiceDefaults;

namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public record GetDailyBalanceResponse(
    DateOnly Date, decimal TotalCredits, decimal TotalDebits,
    decimal Balance, int TransactionCount);

public class GetDailyBalanceEndpoint : ICarterModule
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
            return Results.Ok(await handler.HandleAsync(merchantId, date, ct));
        })
        .WithName("GetDailyBalance")
        .WithSummary("Get daily consolidated balance")
        .WithDescription("Returns the consolidated balance for a given date, including total credits, "
            + "debits, net balance, and transaction count. Returns zeros if no data exists for the date.")
        .Produces<GetDailyBalanceResponse>()
        .CacheOutput("DailyBalance");
    }
}
