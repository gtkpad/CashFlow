namespace CashFlow.Domain.Transactions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken ct = default);
}