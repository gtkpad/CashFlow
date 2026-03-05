using System.Diagnostics.Metrics;

namespace CashFlow.ServiceDefaults;

public sealed class CashFlowMetrics
{
    public const string MeterName = "CashFlow";

    private readonly Counter<long> _transactionsCreated;
    private readonly Histogram<double> _transactionAmount;
    private readonly Counter<long> _consolidationEventsProcessed;
    private readonly Histogram<double> _consolidationProcessingDuration;
    private readonly Histogram<double> _eventualConsistency;
    private readonly Counter<long> _authFailures;

    public CashFlowMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _transactionsCreated = meter.CreateCounter<long>(
            "cashflow.transactions.created", "transactions",
            "Number of transactions created");

        _transactionAmount = meter.CreateHistogram<double>(
            "cashflow.transactions.amount", "{currency}",
            "Transaction amounts");

        _consolidationEventsProcessed = meter.CreateCounter<long>(
            "cashflow.consolidation.events_processed", "events",
            "Number of consolidation events processed");

        _consolidationProcessingDuration = meter.CreateHistogram<double>(
            "cashflow.consolidation.processing_duration_ms", "ms",
            "Consumer processing duration");

        _eventualConsistency = meter.CreateHistogram<double>(
            "cashflow.consolidation.eventual_consistency_ms", "ms",
            "Time from transaction creation to consolidation completion");

        _authFailures = meter.CreateCounter<long>(
            "cashflow.gateway.auth_failures", "failures",
            "Number of authentication failures");
    }

    public void RecordTransactionCreated(string type, string currency)
    {
        _transactionsCreated.Add(1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordTransactionAmount(double amount, string type, string currency)
    {
        _transactionAmount.Record(amount,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordConsolidationEventProcessed(string result)
    {
        _consolidationEventsProcessed.Add(1,
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordConsolidationProcessingDuration(double durationMs)
    {
        _consolidationProcessingDuration.Record(durationMs);
    }

    public void RecordEventualConsistency(double consistencyMs)
    {
        _eventualConsistency.Record(consistencyMs);
    }

    public void RecordAuthFailure(string reason)
    {
        _authFailures.Add(1,
            new KeyValuePair<string, object?>("reason", reason));
    }
}
