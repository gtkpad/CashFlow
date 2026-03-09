using System.Reflection;
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
        var interceptor = new DomainEventInterceptor(() => _publishEndpoint);
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
            DateOnly.FromDateTime(DateTime.UtcNow),
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
            Arg.Any<ITransactionCreated>(), Arg.Any<CancellationToken>());

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
        await _publishEndpoint.DidNotReceiveWithAnyArgs().Publish<ITransactionCreated>(
            default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_PublishThrows_ShouldPropagateException()
    {
        // Arrange
        await using var context = CreateContext();
        _publishEndpoint
            .Publish(Arg.Any<ITransactionCreated>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        var result = Transaction.Create(
            new MerchantId(Guid.NewGuid()),
            DateOnly.FromDateTime(DateTime.UtcNow),
            TransactionType.Credit,
            new Money(100m),
            "Test transaction",
            "user@test.com");

        context.Transactions.Add(result.Value);

        // Act
        var act = () => context.SaveChangesAsync();

        // Assert — exception must propagate, not be swallowed
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("broker down");
    }

    [Fact]
    public async Task SaveChangesAsync_UnmappedDomainEvent_ShouldThrow()
    {
        // Arrange
        await using var context = CreateContext();
        var transaction = Transaction.Create(
            new MerchantId(Guid.NewGuid()),
            DateOnly.FromDateTime(DateTime.UtcNow),
            TransactionType.Credit,
            new Money(100m),
            "Test transaction",
            "user@test.com").Value;

        // Clear the mapped TransactionCreated event and inject an unmapped one via reflection
        transaction.ClearDomainEvents();
        var domainEventsField = typeof(Entity<TransactionId>)
            .GetField("_domainEvents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var events = (List<IDomainEvent>)domainEventsField.GetValue(transaction)!;
        events.Add(new UnmappedDomainEvent());

        context.Transactions.Add(transaction);

        // Act — fail-fast: unmapped events are a programming error, must propagate
        var act = () => context.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No integration event publisher found*");
    }

    private record UnmappedDomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
