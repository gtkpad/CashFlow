using Carter;
using CashFlow.ServiceDefaults;
using FluentValidation;

namespace CashFlow.Transactions.API.Features.CreateTransaction;

public sealed class CreateTransactionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transactions")
            .RequireMerchantId()
            .WithTags("Transactions")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", async (
            CreateTransactionCommand command,
            IValidator<CreateTransactionCommand> validator,
            CreateTransactionHandler handler,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var merchantId = httpContext.GetMerchantId();

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
                return Results.ValidationProblem(validationResult.ToDictionary());

            var result = await handler.HandleAsync(merchantId, command, ct);

            return result.IsSuccess
                ? Results.Created($"/api/v1/transactions/{result.Value.Id}", result.Value)
                : Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Business Rule Violation");
        })
        .WithName("CreateTransaction")
        .WithSummary("Create a financial transaction")
        .WithDescription("Records a credit or debit for the authenticated merchant. "
            + "The transaction is persisted and an event is published via Outbox pattern.")
        .Produces<CreateTransactionResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
