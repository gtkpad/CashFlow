using CashFlow.Domain.Transactions;

namespace CashFlow.Transactions.API.Features.CreateTransaction;

public record CreateTransactionCommand(
    DateOnly ReferenceDate,
    TransactionType Type,
    decimal Amount,
    string Currency,
    string Description,
    string? CreatedBy);
