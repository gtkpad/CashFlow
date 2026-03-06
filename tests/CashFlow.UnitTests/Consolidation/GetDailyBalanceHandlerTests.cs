using CashFlow.Consolidation.API.Features.GetDailyBalance;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.UnitTests.Consolidation;

public class GetDailyBalanceHandlerTests : IDisposable
{
    private readonly ConsolidationDbContext _db;
    private readonly GetDailyBalanceHandler _handler;

    public GetDailyBalanceHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ConsolidationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ConsolidationDbContext(options);
        _handler = new GetDailyBalanceHandler(_db);
    }

    [Fact]
    public async Task HandleAsync_ExistingSummary_ReturnsResponse()
    {
        var merchantId = new MerchantId(Guid.NewGuid());
        var date = new DateOnly(2025, 6, 15);
        var summary = DailySummary.CreateForDay(merchantId, date);
        summary.ApplyTransaction(TransactionType.Credit, new Money(100m, "BRL"));

        _db.DailySummaries.Add(summary);
        await _db.SaveChangesAsync();

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
        var result = await _handler.HandleAsync(Guid.NewGuid(), new DateOnly(2025, 1, 1));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_DifferentMerchant_ReturnsNull()
    {
        var merchantId = new MerchantId(Guid.NewGuid());
        var date = new DateOnly(2025, 6, 15);
        var summary = DailySummary.CreateForDay(merchantId, date);
        summary.ApplyTransaction(TransactionType.Credit, new Money(50m, "BRL"));

        _db.DailySummaries.Add(summary);
        await _db.SaveChangesAsync();

        var otherMerchantId = Guid.NewGuid();
        var result = await _handler.HandleAsync(otherMerchantId, date);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
