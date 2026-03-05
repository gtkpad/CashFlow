using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Features.GetTransaction;

public record GetTransactionResponse(
    Guid Id, Guid MerchantId, DateOnly ReferenceDate, string Type,
    decimal Amount, string Currency, string Description,
    DateTimeOffset CreatedAt, string? CreatedBy);

public class GetTransactionHandler(TransactionsDbContext db)
{
    public async Task<GetTransactionResponse?> HandleAsync(
        Guid merchantId, Guid transactionId, CancellationToken ct = default)
    {
        var txId = new TransactionId(transactionId);
        var mId = new MerchantId(merchantId);
        var transaction = await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == txId && t.MerchantId == mId, ct);

        if (transaction is null)
            return null;

        return new GetTransactionResponse(
            transaction.Id.Value, transaction.MerchantId.Value,
            transaction.ReferenceDate, transaction.Type.ToString(),
            transaction.Value.Amount, transaction.Value.Currency,
            transaction.Description, transaction.CreatedAt, transaction.CreatedBy);
    }
}
