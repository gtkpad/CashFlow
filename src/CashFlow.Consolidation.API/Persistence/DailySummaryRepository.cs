using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Persistence;

public sealed class DailySummaryRepository(ConsolidationDbContext db) : IDailySummaryRepository
{
    public async Task<DailySummary?> GetByDateAndMerchant(
        MerchantId merchantId, DateOnly date, CancellationToken ct = default)
    {
        return await db.DailySummaries
            .FirstOrDefaultAsync(d => d.MerchantId == merchantId && d.Date == date, ct);
    }

    public async Task AddAsync(DailySummary summary, CancellationToken ct = default)
    {
        await db.DailySummaries.AddAsync(summary, ct);
    }
}
