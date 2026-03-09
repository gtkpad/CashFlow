namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public record GetDailyBalanceResponse(
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    int TransactionCount);
