namespace CashFlow.Domain.Transactions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
}
