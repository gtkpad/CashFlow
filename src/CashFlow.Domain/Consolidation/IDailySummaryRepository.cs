using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Consolidation;

public interface IDailySummaryRepository
{
    Task<DailySummary?> GetByDateAndMerchant(MerchantId merchantId, DateOnly date, CancellationToken ct = default);
    Task AddAsync(DailySummary summary, CancellationToken ct = default);
    Task AddIfNewAsync(DailySummary summary, CancellationToken ct = default);
}
