using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Features.CreateTransaction;
using CashFlow.Transactions.API.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CashFlow.UnitTests.Transactions;

public class CreateTransactionHandlerTests
{
    private readonly ITransactionRepository _repository;
    private readonly TransactionsDbContext _dbContext;
    private readonly CreateTransactionHandler _handler;

    public CreateTransactionHandlerTests()
    {
        _repository = Substitute.For<ITransactionRepository>();

        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TransactionsDbContext(options);

        _handler = new CreateTransactionHandler(_repository, _dbContext);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var command = new CreateTransactionCommand(
            ReferenceDate: DateOnly.FromDateTime(DateTime.Today),
            Type: TransactionType.Credit,
            Amount: 150.00m,
            Currency: "BRL",
            Description: "Sale #42",
            CreatedBy: "user@test.com");

        // Act
        var result = await _handler.HandleAsync(merchantId, command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();

        await _repository.Received(1).AddAsync(
            Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyMerchantId_ShouldThrow()
    {
        // Arrange
        var command = new CreateTransactionCommand(
            ReferenceDate: DateOnly.FromDateTime(DateTime.Today),
            Type: TransactionType.Credit,
            Amount: 100.00m,
            Currency: "BRL",
            Description: "Sale #1",
            CreatedBy: "user@test.com");

        // Act
        var act = () => _handler.HandleAsync(Guid.Empty, command);

        // Assert — MerchantId constructor throws before reaching Transaction.Create
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*MerchantId*");
    }
}
