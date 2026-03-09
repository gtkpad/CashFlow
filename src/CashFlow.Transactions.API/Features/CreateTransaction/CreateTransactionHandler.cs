using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.ServiceDefaults;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.API.Features.CreateTransaction;

public sealed class CreateTransactionHandler(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<CreateTransactionHandler> logger,
    CashFlowMetrics metrics)
{
    public async Task<Result<CreateTransactionResponse>> HandleAsync(
        Guid merchantId, CreateTransactionCommand command, CancellationToken ct = default)
    {
        var result = Transaction.Create(
            new MerchantId(merchantId), command.ReferenceDate, command.Type,
            new Money(command.Amount, command.Currency),
            command.Description, command.CreatedBy);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Transaction creation failed for MerchantId {MerchantId}: {Error}",
                merchantId, result.Error);
            return Result.Failure<CreateTransactionResponse>(result.Error!);
        }

        var transaction = result.Value;
        await repository.AddAsync(transaction, ct);
        await unitOfWork.SaveChangesAsync(ct);

        metrics.RecordTransactionCreated(command.Type.ToString(), command.Currency);
        metrics.RecordTransactionAmount((double)command.Amount, command.Type.ToString(), command.Currency);

        logger.LogInformation("Transaction {TransactionId} created for MerchantId {MerchantId}, Amount {Amount} {Currency}",
            transaction.Id.Value, merchantId, command.Amount, command.Currency);

        return Result.Success(new CreateTransactionResponse(
            transaction.Id.Value, transaction.CreatedAt));
    }
}
