using System.Net;
using System.Net.Http.Json;
using CashFlow.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace CashFlow.IntegrationTests.Transactions;

[Collection("IntegrationTests")]
public class CreateTransactionIntegrationTests(TransactionsApiFactory factory)
    : IClassFixture<TransactionsApiFactory>
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    private void SetMerchantId(string merchantId)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Add("X-User-Id", merchantId);
    }

    [Fact]
    public async Task PostTransaction_ValidPayload_ShouldReturn201()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        SetMerchantId(merchantId.ToString());

        var payload = new
        {
            referenceDate = DateOnly.FromDateTime(DateTime.Today),
            type = 1, // Credit
            amount = 150.50m,
            currency = "BRL",
            description = "Integration test sale",
            createdBy = "test-user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/transactions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostTransaction_DebitType_ShouldReturn201()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        SetMerchantId(merchantId.ToString());

        var payload = new
        {
            referenceDate = DateOnly.FromDateTime(DateTime.Today),
            type = 2, // Debit
            amount = 75.25m,
            currency = "BRL",
            description = "Integration test expense",
            createdBy = "test-user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/transactions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostTransaction_MissingMerchantIdHeader_ShouldReturn401()
    {
        // Arrange — no X-User-Id header
        _client.DefaultRequestHeaders.Remove("X-User-Id");

        var payload = new
        {
            referenceDate = DateOnly.FromDateTime(DateTime.Today),
            type = 1,
            amount = 100m,
            currency = "BRL",
            description = "Test",
            createdBy = "test-user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/transactions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTransaction_ZeroAmount_ShouldReturn400()
    {
        // Arrange
        SetMerchantId(Guid.NewGuid().ToString());

        var payload = new
        {
            referenceDate = DateOnly.FromDateTime(DateTime.Today),
            type = 1,
            amount = 0m,
            currency = "BRL",
            description = "Test",
            createdBy = "test-user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/transactions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostTransaction_EmptyDescription_ShouldReturn400()
    {
        // Arrange
        SetMerchantId(Guid.NewGuid().ToString());

        var payload = new
        {
            referenceDate = DateOnly.FromDateTime(DateTime.Today),
            type = 1,
            amount = 50m,
            currency = "BRL",
            description = "",
            createdBy = "test-user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/transactions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record TransactionResponse(Guid Id, DateTimeOffset CreatedAt);
}
