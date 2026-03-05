using CashFlow.ServiceDefaults;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CashFlow.UnitTests.ServiceDefaults;

public class MerchantIdFilterTests
{
    private readonly MerchantIdFilter _filter = new();

    private static EndpointFilterDelegate CreateNext()
        => _ => ValueTask.FromResult<object?>(Results.Ok());

    [Fact]
    public async Task InvokeAsync_ValidGuidHeader_ShouldStoreInItemsAndCallNext()
    {
        var merchantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = merchantId.ToString();
        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        await _filter.InvokeAsync(context, next);

        nextCalled.Should().BeTrue();
        httpContext.Items["MerchantId"].Should().Be(merchantId);
    }

    [Fact]
    public async Task InvokeAsync_MissingHeader_ShouldReturn401()
    {
        var httpContext = new DefaultHttpContext();
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        var result = await _filter.InvokeAsync(context, CreateNext());

        var problemResult = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_InvalidGuidFormat_ShouldReturn401()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "not-a-guid";
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        var result = await _filter.InvokeAsync(context, CreateNext());

        var problemResult = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_EmptyHeader_ShouldReturn401()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "";
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        var result = await _filter.InvokeAsync(context, CreateNext());

        var problemResult = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
