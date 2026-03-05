namespace CashFlow.E2ETests.Infrastructure;

public static class EventualConsistencyHelper
{
    public static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> action,
        Func<T, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        timeout ??= TimeSpan.FromSeconds(15);
        interval ??= TimeSpan.FromMilliseconds(500);
        var deadline = DateTime.UtcNow + timeout.Value;
        T result = default!;

        while (DateTime.UtcNow < deadline)
        {
            result = await action();
            if (predicate(result))
                return result;
            await Task.Delay(interval.Value);
        }

        return result;
    }
}
