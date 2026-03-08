using System.Diagnostics.Metrics;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.ServiceDefaults;
using CashFlow.Transactions.API.Features.CreateTransaction;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CashFlow.UnitTests.Transactions;

public class CreateTransactionHandlerTests
{
    private readonly ITransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateTransactionHandler _handler;

    public CreateTransactionHandlerTests()
    {
        _repository = Substitute.For<ITransactionRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        var meterFactory = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();

        _handler = new CreateTransactionHandler(_repository, _unitOfWork,
            Substitute.For<ILogger<CreateTransactionHandler>>(),
            new CashFlowMetrics(meterFactory));
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

    [Fact]
    public async Task HandleAsync_InvalidDomainData_ShouldReturnFailure()
    {
        // Arrange — zero amount triggers Result.Failure in Transaction.Create
        var merchantId = Guid.NewGuid();
        var command = new CreateTransactionCommand(
            ReferenceDate: DateOnly.FromDateTime(DateTime.Today),
            Type: TransactionType.Credit,
            Amount: 0m,
            Currency: "BRL",
            Description: "Invalid transaction",
            CreatedBy: "user@test.com");

        // Act
        var result = await _handler.HandleAsync(merchantId, command);

        // Assert — handler propagates the domain failure, never calls repository
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("positive");

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }
}
