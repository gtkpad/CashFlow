using CashFlow.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Persistence;

public class TransactionRepository(TransactionsDbContext db) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await db.Transactions.AddAsync(transaction, ct);
    }

    public async Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken ct = default)
    {
        return await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
    }
}
