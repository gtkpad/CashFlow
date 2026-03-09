using CashFlow.Domain.SharedKernel;

namespace CashFlow.Domain.Transactions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task<Transaction?> GetByIdAndMerchantAsync(TransactionId id, MerchantId merchantId, CancellationToken ct = default);
}
