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
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    public async Task InitializeAsync()
    {
        // Pattern from Context7 Aspire.Hosting.Testing docs:
        // https://aspire.dev/docs/testing/write-your-first-test
        using var cts = new CancellationTokenSource(StartupTimeout);
        var ct = cts.Token;

        // Pass SkipDevResources via args: AppHost.cs runs during CreateAsync, so this config
        // must be available before the call (args are injected into builder.Configuration immediately).
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CashFlow_AppHost>(["--AppHost:SkipDevResources=true"], ct);

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

        // Wait for all services to be healthy in parallel before running tests
        await Task.WhenAll(
            _app.ResourceNotifications.WaitForResourceHealthyAsync("identity", ct).WaitAsync(StartupTimeout, ct),
            _app.ResourceNotifications.WaitForResourceHealthyAsync("transactions", ct).WaitAsync(StartupTimeout, ct),
            _app.ResourceNotifications.WaitForResourceHealthyAsync("consolidation", ct).WaitAsync(StartupTimeout, ct),
            _app.ResourceNotifications.WaitForResourceHealthyAsync("gateway", ct).WaitAsync(StartupTimeout, ct)
        );
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
