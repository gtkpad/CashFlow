using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CashFlow.UnitTests.Transactions;

public class DomainEventInterceptorTests
{
    private readonly IPublishEndpoint _publishEndpoint;

    public DomainEventInterceptorTests()
    {
        _publishEndpoint = Substitute.For<IPublishEndpoint>();
    }

    private TransactionsDbContext CreateContext()
    {
        var interceptor = new DomainEventInterceptor(_publishEndpoint);
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new TransactionsDbContext(options);
    }

    [Fact]
    public async Task SaveChangesAsync_WithDomainEvents_ShouldPublishIntegrationEvent()
    {
        // Arrange
        await using var context = CreateContext();
        var result = Transaction.Create(
            new MerchantId(Guid.NewGuid()),
            DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit,
            new Money(100m),
            "Test transaction",
            "user@test.com");

        result.IsSuccess.Should().BeTrue();
        var transaction = result.Value;
        transaction.DomainEvents.Should().HaveCount(1);

        context.Transactions.Add(transaction);

        // Act
        await context.SaveChangesAsync();

        // Assert
        await _publishEndpoint.Received(1).Publish(
            Arg.Any<object>(), typeof(ITransactionCreated), Arg.Any<CancellationToken>());

        transaction.DomainEvents.Should().BeEmpty("interceptor should clear domain events");
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutDomainEvents_ShouldNotPublish()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        await context.SaveChangesAsync();

        // Assert
        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }
}
