using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Persistence;

namespace CashFlow.Transactions.API.Features.CreateTransaction;

public class CreateTransactionHandler(
    ITransactionRepository repository,
    TransactionsDbContext db)
{
    public async Task<Result<CreateTransactionResponse>> HandleAsync(
        Guid merchantId, CreateTransactionCommand command, CancellationToken ct = default)
    {
        var result = Transaction.Create(
            new MerchantId(merchantId), command.ReferenceDate, command.Type,
            new Money(command.Amount, command.Currency),
            command.Description, command.CreatedBy);

        if (!result.IsSuccess)
            return Result.Failure<CreateTransactionResponse>(result.Error!);

        var transaction = result.Value;
        await repository.AddAsync(transaction, ct);
        await db.SaveChangesAsync(ct);

        return Result.Success(new CreateTransactionResponse(
            transaction.Id.Value, transaction.CreatedAt));
    }
}
