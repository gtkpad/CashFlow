using CashFlow.ServiceDefaults;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CashFlow.UnitTests.Gateway;

public class GatewaySecretMiddlewareTests
{
    private readonly ILogger<GatewaySecretMiddleware> _logger = Substitute.For<ILogger<GatewaySecretMiddleware>>();
    private readonly RequestDelegate _next = Substitute.For<RequestDelegate>();

    private GatewaySecretMiddleware CreateMiddleware(
        string? gatewaySecret, string environmentName = "Production")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(gatewaySecret is not null
                ? [new KeyValuePair<string, string?>("Gateway:Secret", gatewaySecret)]
                : [])
            .Build();

        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);

        return new GatewaySecretMiddleware(_next, config, env, _logger);
    }

    [Fact]
    public async Task InvokeAsync_HealthPath_ShouldBypass()
    {
        var middleware = CreateMiddleware("secret");
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_AlivePath_ShouldBypass()
    {
        var middleware = CreateMiddleware("secret");
        var context = new DefaultHttpContext();
        context.Request.Path = "/alive";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_ValidSecret_ShouldCallNext()
    {
        var middleware = CreateMiddleware("my-secret");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";
        context.Request.Headers["X-Gateway-Secret"] = "my-secret";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_InvalidSecret_ShouldReturn403()
    {
        var middleware = CreateMiddleware("correct-secret");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";
        context.Request.Headers["X-Gateway-Secret"] = "wrong-secret";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_MissingSecret_ShouldReturn403()
    {
        var middleware = CreateMiddleware("correct-secret");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_NoConfigInDevelopment_ShouldAllow()
    {
        var middleware = CreateMiddleware(null, "Development");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_NoConfigInProduction_ShouldReturn503()
    {
        var middleware = CreateMiddleware(null, "Production");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        await _next.DidNotReceive().Invoke(context);
    }
}
