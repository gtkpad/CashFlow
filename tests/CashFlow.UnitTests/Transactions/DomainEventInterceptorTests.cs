using System.Reflection;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CashFlow.UnitTests.Transactions;

public class DomainEventInterceptorTests
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IServiceProvider _serviceProvider;

    public DomainEventInterceptorTests()
    {
        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(IPublishEndpoint)).Returns(_publishEndpoint);
    }

    private TransactionsDbContext CreateContext()
    {
        var interceptor = new DomainEventInterceptor(_serviceProvider);
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

    [Fact]
    public async Task SaveChangesAsync_PublishThrows_ShouldPropagateException()
    {
        // Arrange
        await using var context = CreateContext();
        _publishEndpoint
            .Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        var result = Transaction.Create(
            new MerchantId(Guid.NewGuid()),
            DateOnly.FromDateTime(DateTime.Today),
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
    public async Task SaveChangesAsync_UnmappedDomainEvent_ShouldNotPublish()
    {
        // Arrange
        await using var context = CreateContext();
        var transaction = Transaction.Create(
            new MerchantId(Guid.NewGuid()),
            DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Credit,
            new Money(100m),
            "Test transaction",
            "user@test.com").Value;

        // Clear the mapped TransactionCreated event and inject an unmapped one via reflection
        // (Raise is protected on Entity<TId>)
        transaction.ClearDomainEvents();
        var domainEventsField = typeof(Entity<TransactionId>)
            .GetField("_domainEvents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var events = (List<IDomainEvent>)domainEventsField.GetValue(transaction)!;
        events.Add(new UnmappedDomainEvent());

        context.Transactions.Add(transaction);

        // Act
        await context.SaveChangesAsync();

        // Assert — unmapped event returns null from DomainEventMapper, so no publish
        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());

        transaction.DomainEvents.Should().BeEmpty("interceptor should still clear events");
    }

    private record UnmappedDomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
