using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Persistence;

public sealed class DailySummaryRepository(ConsolidationDbContext db) : IDailySummaryRepository
{
    // SEM AsNoTracking: o consumer precisa de change tracking para SaveChangesAsync detectar modificações
    private static readonly Func<ConsolidationDbContext, MerchantId, DateOnly, Task<DailySummary?>>
        _getByDateAndMerchant = EF.CompileAsyncQuery(
            (ConsolidationDbContext ctx, MerchantId merchantId, DateOnly date) =>
                ctx.DailySummaries
                    .FirstOrDefault(d => d.MerchantId == merchantId && d.Date == date));

    public Task<DailySummary?> GetByDateAndMerchant(
        MerchantId merchantId, DateOnly date, CancellationToken ct = default)
        => _getByDateAndMerchant(db, merchantId, date);

    public async Task AddAsync(DailySummary summary, CancellationToken ct = default)
        => await db.DailySummaries.AddAsync(summary, ct);
}
