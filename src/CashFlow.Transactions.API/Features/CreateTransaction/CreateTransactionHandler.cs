using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Persistence;
using MassTransit;

namespace CashFlow.Transactions.API.Features.CreateTransaction;

public class CreateTransactionHandler(
    ITransactionRepository repository,
    TransactionsDbContext db,
    IPublishEndpoint publishEndpoint)
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

        foreach (var domainEvent in transaction.DomainEvents)
        {
            if (domainEvent is TransactionCreated created)
            {
                await publishEndpoint.Publish<ITransactionCreated>(new
                {
                    TransactionId = created.TransactionId.Value,
                    MerchantId = created.MerchantId.Value,
                    ReferenceDate = created.ReferenceDate,
                    TransactionType = created.Type.ToString(),
                    Amount = created.Value.Amount,
                    Currency = created.Value.Currency
                }, ct);
            }
        }

        transaction.ClearDomainEvents();
        await db.SaveChangesAsync(ct);

        return Result.Success(new CreateTransactionResponse(
            transaction.Id.Value, transaction.CreatedAt));
    }
}
