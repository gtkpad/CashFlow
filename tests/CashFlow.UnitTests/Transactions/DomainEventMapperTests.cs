using CashFlow.Domain.IntegrationEvents;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Persistence;
using FluentAssertions;
using MassTransit;
using NSubstitute;

namespace CashFlow.UnitTests.Transactions;

public class DomainEventMapperTests
{
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();

    [Fact]
    public async Task PublishIntegrationEvent_ShouldPreserveDateOnly()
    {
        // Arrange
        var merchantId = new MerchantId(Guid.NewGuid());
        var expectedDate = new DateOnly(2026, 3, 6);
        var transactionCreated = new TransactionCreated
        {
            TransactionId = new TransactionId(Guid.NewGuid()),
            MerchantId = merchantId,
            ReferenceDate = expectedDate,
            Type = TransactionType.Credit,
            Value = new Money(150m)
        };

        ITransactionCreated? captured = null;
        _publishEndpoint
            .Publish(Arg.Do<ITransactionCreated>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await DomainEventMapper.PublishIntegrationEvent(
            transactionCreated, _publishEndpoint, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.ReferenceDate.Should().Be(expectedDate);
        captured.MerchantId.Should().Be(merchantId.Value);
        captured.Amount.Should().Be(150m);
        captured.TransactionType.Should().Be("Credit");
    }

    [Fact]
    public async Task PublishIntegrationEvent_UnmappedEvent_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var unmappedEvent = new UnmappedEvent();

        // Act
        var act = () => DomainEventMapper.PublishIntegrationEvent(
            unmappedEvent, _publishEndpoint, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No integration event publisher found*");
    }

    private record UnmappedEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
