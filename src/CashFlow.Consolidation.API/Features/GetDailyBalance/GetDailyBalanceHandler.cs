using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;

namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public sealed class GetDailyBalanceHandler(IDailySummaryRepository repository)
{
    public async Task<GetDailyBalanceResponse?> HandleAsync(
        Guid merchantId, DateOnly date, CancellationToken ct = default)
    {
        var summary = await repository.FindByDateAndMerchantAsync(new MerchantId(merchantId), date, ct);

        if (summary is null)
            return null;

        return new GetDailyBalanceResponse(
            summary.Date, summary.TotalCredits.Amount, summary.TotalDebits.Amount,
            summary.Balance, summary.TransactionCount);
    }
}
