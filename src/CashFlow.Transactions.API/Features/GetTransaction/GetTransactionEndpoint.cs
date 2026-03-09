using Carter;
using CashFlow.ServiceDefaults;

namespace CashFlow.Transactions.API.Features.GetTransaction;

public sealed class GetTransactionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transactions")
            .RequireMerchantId()
            .WithTags("Transactions")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
                Guid id, HttpContext httpContext,
                GetTransactionHandler handler, CancellationToken ct) =>
            {
                var merchantId = httpContext.GetMerchantId();
                var result = await handler.HandleAsync(merchantId, id, ct);

                return result is not null
                    ? Results.Ok(result)
                    : Results.Problem(
                        $"Transaction {id} not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Resource Not Found");
            })
            .WithName("GetTransaction")
            .WithSummary("Get a transaction by ID")
            .WithDescription("Retrieves the full details of a transaction belonging to the authenticated merchant.")
            .Produces<GetTransactionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
