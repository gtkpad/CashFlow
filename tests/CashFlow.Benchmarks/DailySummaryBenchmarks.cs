using BenchmarkDotNet.Attributes;
using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;

namespace CashFlow.Benchmarks;

[MemoryDiagnoser]
public class DailySummaryBenchmarks
{
    private DailySummary _summary = null!;
    private Money _value = null!;

    [GlobalSetup]
    public void Setup()
    {
        _summary = DailySummary.CreateForDay(MerchantId.New(), DateOnly.FromDateTime(DateTime.Today));
        _value = new Money(100m);
    }

    [Benchmark]
    public void ApplyCredit() => _summary.ApplyTransaction(TransactionType.Credit, _value);

    [Benchmark]
    public void ApplyDebit() => _summary.ApplyTransaction(TransactionType.Debit, _value);
}
