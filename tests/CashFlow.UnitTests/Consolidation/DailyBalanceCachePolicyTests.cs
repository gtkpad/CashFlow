using CashFlow.Consolidation.API.Features.GetDailyBalance;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Time.Testing;

namespace CashFlow.UnitTests.Consolidation;

public class DailyBalanceCachePolicyTests
{
    // Fixed reference point: eliminates flakiness when tests run near UTC midnight
    private static readonly DateTimeOffset ReferenceNow = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _fakeTimeProvider = new(ReferenceNow);
    private readonly DailyBalanceCachePolicy _policy;

    public DailyBalanceCachePolicyTests()
    {
        _policy = new DailyBalanceCachePolicy(_fakeTimeProvider);
    }

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
        var context = CreateContext("2025-06-14");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.ResponseExpirationTimeSpan.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task CacheRequestAsync_TodayDate_ShouldSetFiveSecondExpiration()
    {
        var context = CreateContext("2025-06-15");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.ResponseExpirationTimeSpan.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CacheRequestAsync_FutureDate_ShouldSetFiveSecondExpiration()
    {
        var context = CreateContext("2025-06-16");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.ResponseExpirationTimeSpan.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CacheRequestAsync_ShouldEnableAllCacheFlags()
    {
        var context = CreateContext("2025-01-01");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.EnableOutputCaching.Should().BeTrue();
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
        context.AllowLocking.Should().BeTrue();
    }

    [Fact]
    public async Task CacheRequestAsync_ShouldVaryByUserIdHeader()
    {
        var context = CreateContext("2025-01-01");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.CacheVaryByRules.HeaderNames.ToString().Should().Be("X-User-Id");
    }

    [Fact]
    public async Task CacheRequestAsync_ValidMerchantId_ShouldAddBalanceTag()
    {
        var merchantId = Guid.NewGuid();
        var date = "2025-01-15";
        var context = CreateContext(date, merchantId.ToString());

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.Tags.Should().Contain($"balance-{merchantId}-{date}");
    }

    [Fact]
    public async Task CacheRequestAsync_InvalidMerchantId_ShouldNotAddTag()
    {
        var context = CreateContext("2025-01-15", "not-a-guid");

        await _policy.CacheRequestAsync(context, CancellationToken.None);

        context.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ServeFromCacheAsync_ShouldCompleteSuccessfully()
    {
        var context = CreateContext("2025-01-01");

        await _policy.ServeFromCacheAsync(context, CancellationToken.None);

        context.AllowCacheStorage.Should().BeFalse("no-op should not modify context");
    }

    [Fact]
    public async Task ServeResponseAsync_ShouldCompleteSuccessfully()
    {
        var context = CreateContext("2025-01-01");

        await _policy.ServeResponseAsync(context, CancellationToken.None);

        context.AllowCacheStorage.Should().BeFalse("no-op should not modify context");
    }
}
