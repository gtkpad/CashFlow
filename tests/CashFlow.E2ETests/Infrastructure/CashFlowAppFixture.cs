using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CashFlow.E2ETests.Infrastructure;

public class CashFlowAppFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;

    public DistributedApplication App => _app;

    /// <summary>
    /// Maximum time to wait for the AppHost and all resources to start.
    /// </summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(15);

    public async Task InitializeAsync()
    {
        // Pattern from Context7 Aspire.Hosting.Testing docs:
        // https://aspire.dev/docs/testing/write-your-first-test
        using var cts = new CancellationTokenSource(StartupTimeout);
        var ct = cts.Token;

        // Signal AppHost to skip dev-only resources (pgAdmin, RabbitMQ management UI, data volumes).
        // Must be set BEFORE CreateAsync because AppHost.cs runs (and reads config) during that call.
        // Using environment variable ensures reliable detection regardless of arg parser behavior.
        Environment.SetEnvironmentVariable("CASHFLOW_E2E_TESTING", "true");

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CashFlow_AppHost>(ct);

        // Provide values for secret parameters not available in CI environment.
        // Parameters are resolved lazily (during StartAsync), so post-CreateAsync injection works.
        appHost.Configuration["Parameters:jwt-signing-key"] = "E2eTests_JwtSigningKey_AtLeast32Chars!!";
        appHost.Configuration["Parameters:gateway-secret"] = "E2eTests_GatewaySecret";

        // Configure logging (recommended by Aspire testing docs)
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        // Build and start with timeout safety (Context7 pattern)
        _app = await appHost.BuildAsync(ct)
            .WaitAsync(StartupTimeout, ct);
        await _app.StartAsync(ct)
            .WaitAsync(StartupTimeout, ct);

        // StartAsync returns when all resources reach their target state:
        // - Containers (postgres, rabbitmq): Running
        // - Services without WithHttpHealthCheck (E2E mode): Running
        // - Services with WithHttpHealthCheck (dev mode): Healthy
        // In E2E mode, WithHttpHealthCheck is omitted and gateway uses WaitForStart,
        // so StartAsync returns as soon as all services are Running.
        // Functional readiness is verified by the E2E test assertions themselves.
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class CashFlowE2ECollection : ICollectionFixture<CashFlowAppFixture>
{
    public const string Name = "CashFlowE2E";
}
