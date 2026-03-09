using System.Diagnostics.Metrics;
using System.Security.Claims;
using CashFlow.Gateway.Middleware;
using CashFlow.ServiceDefaults;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CashFlow.UnitTests.Gateway;

public class AuthMiddlewareTests
{
    private readonly RequestDelegate _next = Substitute.For<RequestDelegate>();

    private AuthMiddleware CreateMiddleware(string? gatewaySecret = "gw-secret")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(gatewaySecret is not null
                ? [new KeyValuePair<string, string?>("Gateway:Secret", gatewaySecret)]
                : [])
            .Build();

        var meterFactory = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();

        return new AuthMiddleware(_next, config, Substitute.For<ILogger<AuthMiddleware>>(),
            new CashFlowMetrics(meterFactory));
    }

    [Fact]
    public async Task InvokeAsync_PublicPath_ShouldBypassAuth()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/identity/login";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_HealthPath_ShouldBypassAuth()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_ShouldReturn401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ShouldInjectUserId()
    {
        var middleware = CreateMiddleware();
        var userId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", userId)], "test"))
        };
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-User-Id"].ToString().Should().Be(userId);
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ShouldInjectGatewaySecret()
    {
        var middleware = CreateMiddleware("my-gw-secret");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "user-1")], "test"))
        };
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-Gateway-Secret"].ToString().Should().Be("my-gw-secret");
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_PublicPath_ShouldStillInjectGatewaySecret()
    {
        var middleware = CreateMiddleware("my-gw-secret");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/identity/login";

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-Gateway-Secret"].ToString().Should().Be("my-gw-secret");
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_ShouldReturn401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";
        context.Request.Headers["Authorization"] = "Bearer invalid-token";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_UserWithNameIdentifierClaim_ShouldFallback()
    {
        var middleware = CreateMiddleware();
        var userId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
        };
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-User-Id"].ToString().Should().Be(userId);
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_PreExistingXUserIdHeader_ShouldBeReplacedWithJwtSub()
    {
        var middleware = CreateMiddleware();
        var realUserId = Guid.NewGuid().ToString();
        var spoofedUserId = Guid.NewGuid().ToString();

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", realUserId)], "test"))
        };
        context.Request.Path = "/api/transactions";
        context.Request.Headers["X-User-Id"] = spoofedUserId;

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-User-Id"].ToString().Should().Be(realUserId);
        context.Request.Headers["X-User-Id"].ToString().Should().NotBe(spoofedUserId);
        await _next.Received(1).Invoke(context);
    }
}
