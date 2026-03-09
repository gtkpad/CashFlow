using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Persistence;

public sealed class DailySummaryRepository(ConsolidationDbContext db) : IDailySummaryRepository
{
    // With tracking: consumer needs change tracking so SaveChangesAsync detects modifications
    public Task<DailySummary?> GetByDateAndMerchantAsync(
        MerchantId merchantId, DateOnly date, CancellationToken ct = default)
        => db.DailySummaries
            .FirstOrDefaultAsync(d => d.MerchantId == merchantId && d.Date == date, ct);

    // Without tracking: pure read — avoids ChangeTracker overhead
    public Task<DailySummary?> FindByDateAndMerchantAsync(
        MerchantId merchantId, DateOnly date, CancellationToken ct = default)
        => db.DailySummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MerchantId == merchantId && d.Date == date, ct);

    public Task AddIfNewAsync(DailySummary summary, CancellationToken ct = default)
    {
        if (db.Entry(summary).State == EntityState.Detached)
            db.DailySummaries.Add(summary);

        return Task.CompletedTask;
    }
}
