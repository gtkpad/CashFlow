using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public sealed class GetDailyBalanceHandler(ConsolidationDbContext db)
{
    private static readonly Func<ConsolidationDbContext, MerchantId, DateOnly, Task<DailySummary?>>
        _query = EF.CompileAsyncQuery(
            (ConsolidationDbContext ctx, MerchantId merchantId, DateOnly date) =>
                ctx.DailySummaries
                    .AsNoTracking()
                    .FirstOrDefault(d => d.MerchantId == merchantId && d.Date == date));

    public async Task<GetDailyBalanceResponse?> HandleAsync(
        Guid merchantId, DateOnly date, CancellationToken ct = default)
    {
        var summary = await _query(db, new MerchantId(merchantId), date);

        if (summary is null)
            return null;

        return new GetDailyBalanceResponse(
            summary.Date, summary.TotalCredits.Amount, summary.TotalDebits.Amount,
            summary.Balance, summary.TransactionCount);
    }
}
