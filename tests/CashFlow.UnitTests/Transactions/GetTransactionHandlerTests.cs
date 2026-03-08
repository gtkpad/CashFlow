using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Features.GetTransaction;
using CashFlow.Transactions.API.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.UnitTests.Transactions;

public class GetTransactionHandlerTests : IDisposable
{
    private readonly TransactionsDbContext _dbContext;
    private readonly GetTransactionHandler _handler;
    private readonly Guid _merchantId = Guid.NewGuid();

    public GetTransactionHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TransactionsDbContext(options);
        _handler = new GetTransactionHandler(_dbContext);
    }

    [Fact]
    public async Task HandleAsync_ExistingTransaction_ShouldReturnResponse()
    {
        var transaction = Transaction.Create(
            new MerchantId(_merchantId), DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(150m), "Sale #1", "user@test.com").Value;

        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(_merchantId, transaction.Id.Value);

        result.Should().NotBeNull();
        result!.Id.Should().Be(transaction.Id.Value);
        result.Amount.Should().Be(150m);
        result.MerchantId.Should().Be(_merchantId);
    }

    [Fact]
    public async Task HandleAsync_NonExistentId_ShouldReturnNull()
    {
        var result = await _handler.HandleAsync(_merchantId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_TransactionOfDifferentMerchant_ShouldReturnNull()
    {
        var otherMerchantId = Guid.NewGuid();
        var transaction = Transaction.Create(
            new MerchantId(otherMerchantId), DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Debit, new Money(75m), "Other merchant", "user@test.com").Value;

        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(_merchantId, transaction.Id.Value);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
