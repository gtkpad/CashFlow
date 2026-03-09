using System.Diagnostics.Metrics;
using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Domain.IntegrationEvents;
using CashFlow.ServiceDefaults;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CashFlow.UnitTests.Consolidation;

public class TransactionFaultConsumerTests
{
    private readonly TransactionFaultConsumer _consumer;
    private readonly CashFlowMetrics _metrics;

    public TransactionFaultConsumerTests()
    {
        var meterFactory = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();
        _metrics = new CashFlowMetrics(meterFactory);
        _consumer = new TransactionFaultConsumer(
            _metrics, NullLogger<TransactionFaultConsumer>.Instance);
    }

    [Fact]
    public async Task Consume_ShouldCompleteWithoutException()
    {
        var context = CreateFaultContext("System.InvalidOperationException", "broker down");

        var act = () => _consumer.Consume(context);

        await act.Should().NotThrowAsync("fault consumers must not propagate exceptions");
    }

    [Fact]
    public async Task Consume_ShouldNotThrow_WhenNoExceptions()
    {
        var context = CreateFaultContext(null, null);

        var act = () => _consumer.Consume(context);

        await act.Should().NotThrowAsync();
    }

    private static ConsumeContext<Fault<ITransactionCreated>> CreateFaultContext(
        string? exceptionType, string? message)
    {
        var exceptionInfo = Substitute.For<ExceptionInfo>();
        exceptionInfo.ExceptionType.Returns(exceptionType ?? "Unknown");
        exceptionInfo.Message.Returns(message ?? string.Empty);

        var fault = Substitute.For<Fault<ITransactionCreated>>();
        fault.FaultedMessageId.Returns(Guid.NewGuid());
        fault.Exceptions.Returns([exceptionInfo]);

        var context = Substitute.For<ConsumeContext<Fault<ITransactionCreated>>>();
        context.Message.Returns(fault);

        return context;
    }
}
