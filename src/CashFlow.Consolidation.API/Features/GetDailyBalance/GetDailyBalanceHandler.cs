using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public class GetDailyBalanceHandler(ConsolidationDbContext db)
{
    public async Task<GetDailyBalanceResponse?> HandleAsync(
        Guid merchantId, DateOnly date, CancellationToken ct = default)
    {
        var mId = new MerchantId(merchantId);
        var summary = await db.DailySummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MerchantId == mId && d.Date == date, ct);

        if (summary is null)
            return null;

        return new GetDailyBalanceResponse(
            summary.Date, summary.TotalCredits.Amount, summary.TotalDebits.Amount,
            summary.Balance.Amount, summary.TransactionCount);
    }
}
