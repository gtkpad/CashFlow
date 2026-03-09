using CashFlow.Consolidation.API.Features.GetDailyBalance;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace CashFlow.UnitTests.Consolidation;

public class DailyBalanceCachePolicyTests
{
    private readonly DailyBalanceCachePolicy _policy = new(TimeProvider.System);

    private static OutputCacheContext CreateContext(string? date = null, string? userId = null)
    {
        var httpContext = new DefaultHttpContext();

        if (date is not null)
            httpContext.Request.RouteValues["date"] = date;

        if (userId is not null)
            httpContext.Request.Headers["X-User-Id"] = userId;

        return new OutputCacheContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task CacheRequestAsync_PastDate_ShouldSetOneHourExpiration()
    {
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd");
        var context = CreateContext(date: pastDate);

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.ResponseExpirationTimeSpan.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task CacheRequestAsync_TodayDate_ShouldSetFiveSecondExpiration()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var context = CreateContext(date: today);

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.ResponseExpirationTimeSpan.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CacheRequestAsync_FutureDate_ShouldSetFiveSecondExpiration()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");
        var context = CreateContext(date: futureDate);

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.ResponseExpirationTimeSpan.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CacheRequestAsync_ShouldEnableAllCacheFlags()
    {
        var context = CreateContext(date: "2025-01-01");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.EnableOutputCaching.Should().BeTrue();
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
        context.AllowLocking.Should().BeTrue();
    }

    [Fact]
    public async Task CacheRequestAsync_ShouldVaryByUserIdHeader()
    {
        var context = CreateContext(date: "2025-01-01");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.CacheVaryByRules.HeaderNames.ToString().Should().Be("X-User-Id");
    }

    [Fact]
    public async Task CacheRequestAsync_ValidMerchantId_ShouldAddBalanceTag()
    {
        var merchantId = Guid.NewGuid();
        var date = "2025-01-15";
        var context = CreateContext(date: date, userId: merchantId.ToString());

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.Tags.Should().Contain($"balance-{merchantId}-{date}");
    }

    [Fact]
    public async Task CacheRequestAsync_InvalidMerchantId_ShouldNotAddTag()
    {
        var context = CreateContext(date: "2025-01-15", userId: "not-a-guid");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ServeFromCacheAsync_ShouldCompleteSuccessfully()
    {
        var context = CreateContext(date: "2025-01-01");

        await _policy.ServeFromCacheAsync(context, CancellationToken.None);

        context.AllowCacheStorage.Should().BeFalse("no-op should not modify context");
    }

    [Fact]
    public async Task ServeResponseAsync_ShouldCompleteSuccessfully()
    {
        var context = CreateContext(date: "2025-01-01");

        await _policy.ServeResponseAsync(context, CancellationToken.None);

        context.AllowCacheStorage.Should().BeFalse("no-op should not modify context");
    }
}
