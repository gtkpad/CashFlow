using CashFlow.Domain.Transactions;

namespace CashFlow.Transactions.API.Persistence;

public sealed class TransactionRepository(TransactionsDbContext db) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await db.Transactions.AddAsync(transaction, ct);
    }
}
