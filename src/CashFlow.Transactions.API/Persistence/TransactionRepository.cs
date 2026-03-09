using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Persistence;

public sealed class TransactionRepository(TransactionsDbContext db) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken ct = default) =>
        await db.Transactions.AddAsync(transaction, ct);

    public async Task<Transaction?> GetByIdAndMerchantAsync(
        TransactionId id, MerchantId merchantId, CancellationToken ct = default)
        => await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId, ct);
}
