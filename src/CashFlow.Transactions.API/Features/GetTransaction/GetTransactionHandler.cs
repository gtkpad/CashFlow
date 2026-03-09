using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;

namespace CashFlow.Transactions.API.Features.GetTransaction;

public record GetTransactionResponse(
    Guid Id,
    Guid MerchantId,
    DateOnly ReferenceDate,
    string Type,
    decimal Amount,
    string Currency,
    string Description,
    DateTimeOffset CreatedAt,
    string? CreatedBy);

public sealed class GetTransactionHandler(ITransactionRepository repository)
{
    public async Task<GetTransactionResponse?> HandleAsync(
        Guid merchantId, Guid transactionId, CancellationToken ct = default)
    {
        var transaction = await repository.GetByIdAndMerchantAsync(
            new TransactionId(transactionId), new MerchantId(merchantId), ct);

        if (transaction is null)
            return null;

        return new GetTransactionResponse(
            transaction.Id.Value, transaction.MerchantId.Value,
            transaction.ReferenceDate, transaction.Type.ToString(),
            transaction.Value.Amount, transaction.Value.Currency,
            transaction.Description, transaction.CreatedAt, transaction.CreatedBy);
    }
}
