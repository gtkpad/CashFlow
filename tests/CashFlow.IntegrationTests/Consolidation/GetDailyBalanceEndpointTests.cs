using System.Net;
using System.Net.Http.Json;
using CashFlow.Consolidation.API.Features.GetDailyBalance;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using CashFlow.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.IntegrationTests.Consolidation;

[Collection("IntegrationTests")]
public class GetDailyBalanceEndpointTests(ConsolidationApiFactory factory)
    : IClassFixture<ConsolidationApiFactory>
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    private void SetMerchantId(string merchantId)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Add("X-User-Id", merchantId);
    }

    private async Task SeedSummaryAsync(Guid merchantId, DateOnly date, decimal creditAmount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

        var summary = DailySummary.CreateForDay(new MerchantId(merchantId), date);
        summary.ApplyTransaction(TransactionType.Credit, new Money(creditAmount));
        db.DailySummaries.Add(summary);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDailyBalance_NoMerchantIdHeader_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/v1/consolidation/2025-06-01");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDailyBalance_InvalidDateFormat_ShouldReturn400()
    {
        SetMerchantId(Guid.NewGuid().ToString());

        var response = await _client.GetAsync("/api/v1/consolidation/not-a-date");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDailyBalance_NoDataForDate_ShouldReturn404()
    {
        SetMerchantId(Guid.NewGuid().ToString());

        var response = await _client.GetAsync("/api/v1/consolidation/2025-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Be("The requested consolidated balance was not found.");
        problem.Title.Should().Be("Resource Not Found");
    }

    [Fact]
    public async Task GetDailyBalance_ExistingData_ShouldReturn200WithCorrectBody()
    {
        var merchantId = Guid.NewGuid();
        var date = new DateOnly(2025, 3, 10);
        SetMerchantId(merchantId.ToString());
        await SeedSummaryAsync(merchantId, date, 250m);

        var response = await _client.GetAsync($"/api/v1/consolidation/{date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDailyBalanceResponse>();
        result.Should().NotBeNull();
        result!.Date.Should().Be(date);
        result.TotalCredits.Should().Be(250m);
        result.TotalDebits.Should().Be(0m);
        result.Balance.Should().Be(250m);
        result.TransactionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyBalance_PastDate_SecondRequestShouldBeIdempotent()
    {
        var merchantId = Guid.NewGuid();
        var pastDate = new DateOnly(2024, 12, 1);
        SetMerchantId(merchantId.ToString());
        await SeedSummaryAsync(merchantId, pastDate, 100m);

        var first = await _client.GetAsync($"/api/v1/consolidation/{pastDate:yyyy-MM-dd}");
        var second = await _client.GetAsync($"/api/v1/consolidation/{pastDate:yyyy-MM-dd}");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.TryGetValues("Age", out _).Should().BeTrue(
            "output cache should emit the Age header for cached responses");
    }

    private record ProblemDetailsResponse(string? Detail, string? Title);
}
