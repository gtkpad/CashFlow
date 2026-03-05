using System.Diagnostics.Metrics;

namespace CashFlow.ServiceDefaults;

public sealed class CashFlowMetrics
{
    public const string MeterName = "CashFlow";

    private static readonly Meter Meter = new(MeterName);

    // Transactions
    private static readonly Counter<long> TransactionsCreated =
        Meter.CreateCounter<long>("cashflow.transactions.created", "transactions",
            "Number of transactions created");

    private static readonly Histogram<double> TransactionAmount =
        Meter.CreateHistogram<double>("cashflow.transactions.amount", "BRL",
            "Transaction amounts");

    // Consolidation
    private static readonly Counter<long> ConsolidationEventsProcessed =
        Meter.CreateCounter<long>("cashflow.consolidation.events_processed", "events",
            "Number of consolidation events processed");

    private static readonly Histogram<double> ConsolidationProcessingDuration =
        Meter.CreateHistogram<double>("cashflow.consolidation.processing_duration_ms", "ms",
            "Consumer processing duration");

    private static readonly Histogram<double> EventualConsistency =
        Meter.CreateHistogram<double>("cashflow.consolidation.eventual_consistency_ms", "ms",
            "Time from transaction creation to consolidation completion");

    // Gateway
    private static readonly Counter<long> AuthFailures =
        Meter.CreateCounter<long>("cashflow.gateway.auth_failures", "failures",
            "Number of authentication failures");

    public void RecordTransactionCreated(string type, string currency)
    {
        TransactionsCreated.Add(1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordTransactionAmount(double amount, string type, string currency)
    {
        TransactionAmount.Record(amount,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordConsolidationEventProcessed(string result)
    {
        ConsolidationEventsProcessed.Add(1,
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordConsolidationProcessingDuration(double durationMs)
    {
        ConsolidationProcessingDuration.Record(durationMs);
    }

    public void RecordEventualConsistency(double consistencyMs)
    {
        EventualConsistency.Record(consistencyMs);
    }

    public void RecordAuthFailure(string reason)
    {
        AuthFailures.Add(1,
            new KeyValuePair<string, object?>("reason", reason));
    }
}
