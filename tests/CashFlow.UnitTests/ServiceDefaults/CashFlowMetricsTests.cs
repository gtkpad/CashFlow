using System.Diagnostics.Metrics;
using CashFlow.ServiceDefaults;
using FluentAssertions;

namespace CashFlow.UnitTests.ServiceDefaults;

public class CashFlowMetricsTests
{
    private readonly CashFlowMetrics _metrics = new();

    [Fact]
    public void RecordTransactionCreated_ShouldIncrementCounter()
    {
        using var listener = new MeterListener();
        long count = 0;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "cashflow.transactions.created")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        _metrics.RecordTransactionCreated("Credit", "BRL");

        count.Should().Be(1);
    }

    [Fact]
    public void RecordTransactionAmount_ShouldRecordValue()
    {
        using var listener = new MeterListener();
        double recorded = 0;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "cashflow.transactions.amount")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => recorded = measurement);
        listener.Start();

        _metrics.RecordTransactionAmount(150.50, "Credit", "BRL");

        recorded.Should().Be(150.50);
    }

    [Fact]
    public void RecordConsolidationEventProcessed_ShouldIncrementCounter()
    {
        using var listener = new MeterListener();
        long count = 0;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "cashflow.consolidation.events_processed")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        _metrics.RecordConsolidationEventProcessed("success");

        count.Should().Be(1);
    }

    [Fact]
    public void RecordConsolidationProcessingDuration_ShouldRecordValue()
    {
        using var listener = new MeterListener();
        double recorded = 0;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "cashflow.consolidation.processing_duration_ms")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => recorded = measurement);
        listener.Start();

        _metrics.RecordConsolidationProcessingDuration(42.5);

        recorded.Should().Be(42.5);
    }

    [Fact]
    public void RecordEventualConsistency_ShouldRecordValue()
    {
        using var listener = new MeterListener();
        double recorded = 0;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "cashflow.consolidation.eventual_consistency_ms")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => recorded = measurement);
        listener.Start();

        _metrics.RecordEventualConsistency(1030.0);

        recorded.Should().Be(1030.0);
    }

    [Fact]
    public void RecordAuthFailure_ShouldIncrementCounter()
    {
        using var listener = new MeterListener();
        long count = 0;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "cashflow.gateway.auth_failures")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        _metrics.RecordAuthFailure("unauthorized");

        count.Should().Be(1);
    }
}
