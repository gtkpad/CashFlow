using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Consolidation;

public interface IDailySummaryRepository
{
    Task<DailySummary?> GetByDateAndMerchantAsync(MerchantId merchantId, DateOnly date, CancellationToken ct = default);
    Task<DailySummary?> FindByDateAndMerchantAsync(MerchantId merchantId, DateOnly date, CancellationToken ct = default);
    Task AddIfNewAsync(DailySummary summary, CancellationToken ct = default);
}
