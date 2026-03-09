namespace CashFlow.Transactions.API.Features.GetTransaction;

public record GetTransactionResponse(
    Guid Id,
    Guid MerchantId,
    DateOnly ReferenceDate,
    string Type,
    decimal Amount,
    string Currency,
    string Description,
    DateTimeOffset CreatedAt,
    string? CreatedBy);
