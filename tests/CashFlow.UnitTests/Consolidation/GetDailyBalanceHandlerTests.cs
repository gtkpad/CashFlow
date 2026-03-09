using CashFlow.Consolidation.API.Features.GetDailyBalance;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using FluentAssertions;
using NSubstitute;

namespace CashFlow.UnitTests.Consolidation;

public class GetDailyBalanceHandlerTests
{
    private readonly GetDailyBalanceHandler _handler;
    private readonly IDailySummaryRepository _repository;

    public GetDailyBalanceHandlerTests()
    {
        _repository = Substitute.For<IDailySummaryRepository>();
        _handler = new GetDailyBalanceHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ExistingSummary_ReturnsResponse()
    {
        var merchantId = new MerchantId(Guid.NewGuid());
        var date = new DateOnly(2025, 6, 15);
        var summary = DailySummary.CreateForDay(merchantId, date);
        summary.ApplyTransaction(TransactionType.Credit, new Money(100m));

        _repository
            .FindByDateAndMerchantAsync(merchantId, date, Arg.Any<CancellationToken>())
            .Returns(summary);

        var result = await _handler.HandleAsync(merchantId.Value, date);

        result.Should().NotBeNull();
        result!.Date.Should().Be(date);
        result.TotalCredits.Should().Be(100m);
        result.TotalDebits.Should().Be(0m);
        result.Balance.Should().Be(100m);
        result.TransactionCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_NoSummaryExists_ReturnsNull()
    {
        _repository
            .FindByDateAndMerchantAsync(Arg.Any<MerchantId>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), new DateOnly(2025, 1, 1));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_DifferentMerchant_ReturnsNull()
    {
        _repository
            .FindByDateAndMerchantAsync(Arg.Any<MerchantId>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), new DateOnly(2025, 6, 15));

        result.Should().BeNull();
    }
}
