using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Features.GetTransaction;
using FluentAssertions;
using NSubstitute;

namespace CashFlow.UnitTests.Transactions;

public class GetTransactionHandlerTests
{
    private readonly GetTransactionHandler _handler;
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly ITransactionRepository _repository;

    public GetTransactionHandlerTests()
    {
        _repository = Substitute.For<ITransactionRepository>();
        _handler = new GetTransactionHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ExistingTransaction_ShouldReturnResponse()
    {
        var transaction = Transaction.Create(
            new MerchantId(_merchantId), DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit, new Money(150m), "Sale #1", "user@test.com").Value;

        _repository
            .GetByIdAndMerchantAsync(transaction.Id, new MerchantId(_merchantId), Arg.Any<CancellationToken>())
            .Returns(transaction);

        var result = await _handler.HandleAsync(_merchantId, transaction.Id.Value);

        result.Should().NotBeNull();
        result!.Id.Should().Be(transaction.Id.Value);
        result.Amount.Should().Be(150m);
        result.MerchantId.Should().Be(_merchantId);
    }

    [Fact]
    public async Task HandleAsync_NonExistentId_ShouldReturnNull()
    {
        _repository
            .GetByIdAndMerchantAsync(Arg.Any<TransactionId>(), Arg.Any<MerchantId>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var result = await _handler.HandleAsync(_merchantId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_TransactionOfDifferentMerchant_ShouldReturnNull()
    {
        _repository
            .GetByIdAndMerchantAsync(Arg.Any<TransactionId>(), Arg.Any<MerchantId>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var result = await _handler.HandleAsync(_merchantId, Guid.NewGuid());

        result.Should().BeNull();
    }
}
