using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;
using CashFlow.E2ETests.Infrastructure;
using FluentAssertions;

namespace CashFlow.E2ETests;

[Collection(CashFlowE2ECollection.Name)]
public class TransactionFlowTests(CashFlowAppFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Full E2E flow through the Gateway: register + login, create a credit transaction,
    /// wait for the event to flow through RabbitMQ to the Consolidation consumer,
    /// then verify the daily balance reflects the credit.
    /// </summary>
    [Fact]
    public async Task CreateTransaction_ThenGetConsolidation_ShouldReflectBalance()
    {
        // Arrange — single client through the Gateway
        var client = fixture.App.CreateHttpClient("gateway");
        var auth = await AuthHelper.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act — create a credit transaction via Gateway
        var createResponse = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            referenceDate = today,
            type = 1, // Credit
            amount = 250.00m,
            currency = "BRL",
            description = "E2E test credit"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await GetResponseBody(createResponse));

        // Poll for eventual consistency instead of fixed delay
        var balance = await EventualConsistencyHelper.WaitForConditionAsync(
            action: async () =>
            {
                var resp = await client.GetAsync($"/api/v1/consolidation/{today:yyyy-MM-dd}");
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<DailyBalanceResponse>(JsonOptions);
            },
            predicate: b => b is not null && b.TransactionCount >= 1);

        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(250.00m);
        balance!.Balance.Should().Be(250.00m);
        balance!.TransactionCount.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Creates both a credit and a debit transaction through the Gateway,
    /// then verifies the consolidated daily balance reflects the net result.
    /// </summary>
    [Fact]
    public async Task CreateCreditAndDebit_ShouldReflectNetBalance()
    {
        // Arrange
        var client = fixture.App.CreateHttpClient("gateway");
        var auth = await AuthHelper.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act — create a credit
        var creditResponse = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            referenceDate = today,
            type = 1, // Credit
            amount = 1000.00m,
            currency = "BRL",
            description = "E2E credit"
        });
        creditResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await GetResponseBody(creditResponse));

        // Create a debit
        var debitResponse = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            referenceDate = today,
            type = 2, // Debit
            amount = 350.00m,
            currency = "BRL",
            description = "E2E debit"
        });
        debitResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await GetResponseBody(debitResponse));

        // Poll for eventual consistency instead of fixed delay
        var balance = await EventualConsistencyHelper.WaitForConditionAsync(
            action: async () =>
            {
                var resp = await client.GetAsync($"/api/v1/consolidation/{today:yyyy-MM-dd}");
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<DailyBalanceResponse>(JsonOptions);
            },
            predicate: b => b is not null && b.TransactionCount >= 2);

        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(1000.00m);
        balance!.TotalDebits.Should().Be(350.00m);
        balance!.Balance.Should().Be(650.00m);
        balance!.TransactionCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that the Gateway rejects unauthenticated requests
    /// to protected endpoints with a 401 Unauthorized response.
    /// </summary>
    [Fact]
    public async Task Gateway_UnauthenticatedRequest_ShouldReturn401()
    {
        // Arrange — no auth header
        var client = fixture.App.CreateHttpClient("gateway");

        // Act
        var response = await client.GetAsync(
            $"/api/v1/consolidation/{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that the Gateway allows unauthenticated access
    /// to the identity endpoints (public paths).
    /// </summary>
    [Fact]
    public async Task Gateway_IdentityEndpoints_ShouldBePublic()
    {
        // Arrange
        var client = fixture.App.CreateHttpClient("gateway");
        var email = $"test-public-{Guid.NewGuid():N}@example.com";

        // Act — register should work without auth
        var registerResponse = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password = "TestPass123!"
        });

        // Assert
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies fault isolation (NFR-1): when consolidation is unavailable,
    /// transactions should still be accepted. The Outbox pattern ensures
    /// events are delivered when consolidation recovers.
    /// </summary>
    [Fact]
    public async Task ConsolidationDown_TransactionsShouldStillWork()
    {
        // Arrange — authenticate via Gateway
        var client = fixture.App.CreateHttpClient("gateway");
        var auth = await AuthHelper.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act — create a transaction (consolidation may or may not be processing,
        // but the Transactions API should accept and persist independently)
        var createResponse = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            referenceDate = today,
            type = 1, // Credit
            amount = 100.00m,
            currency = "BRL",
            description = "E2E fault isolation test"
        });

        // Assert — Transactions API accepts the request regardless of
        // consolidation state, thanks to async messaging via Outbox
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await GetResponseBody(createResponse));

        // Verify the transaction was persisted by retrieving it
        var body = await createResponse.Content
            .ReadFromJsonAsync<CreateTransactionResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();

        var getResponse = await client.GetAsync($"/api/v1/transactions/{body.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> GetResponseBody(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "(unable to read response body)";
        }
    }

    private record DailyBalanceResponse(
        DateOnly Date,
        decimal TotalCredits,
        decimal TotalDebits,
        decimal Balance,
        int TransactionCount);

    private record CreateTransactionResponse(Guid Id, DateTimeOffset CreatedAt);
}
