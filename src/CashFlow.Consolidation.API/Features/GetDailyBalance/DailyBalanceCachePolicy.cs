using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace CashFlow.Consolidation.API.Features.GetDailyBalance;

public sealed class DailyBalanceCachePolicy(TimeProvider timeProvider) : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        context.AllowLocking = true;

        var dateStr = context.HttpContext.Request.RouteValues["date"]?.ToString();
        var isPastDate = DateOnly.TryParse(dateStr, out var date)
                         && date < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        context.ResponseExpirationTimeSpan = isPastDate
            ? TimeSpan.FromHours(1)
            : TimeSpan.FromSeconds(5);

        context.CacheVaryByRules.HeaderNames = new StringValues("X-User-Id");

        var userIdRaw = context.HttpContext.Request.Headers["X-User-Id"].ToString();
        if (Guid.TryParse(userIdRaw, out var merchantId) && dateStr is not null)
            context.Tags.Add($"balance-{merchantId}-{dateStr}");

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
        => ValueTask.CompletedTask;
}
