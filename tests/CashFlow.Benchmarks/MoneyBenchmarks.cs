using BenchmarkDotNet.Attributes;
using CashFlow.Domain.SharedKernel;

namespace CashFlow.Benchmarks;

[MemoryDiagnoser]
public class MoneyBenchmarks
{
    private Money _a = null!;
    private Money _b = null!;

    [GlobalSetup]
    public void Setup()
    {
        _a = new Money(150.75m);
        _b = new Money(49.25m);
    }

    [Benchmark]
    public Money Addition() => _a + _b;

    [Benchmark]
    public Money Subtraction() => _a - _b;
}
