using CashFlow.Consolidation.API.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace CashFlow.IntegrationTests.Infrastructure;

// Uses ConsolidationDbContext as anchor type since both APIs define
// a global 'Program' class, causing ambiguity at compile time.
public class ConsolidationApiFactory : WebApplicationFactory<ConsolidationDbContext>, IAsyncLifetime
{
    private const string GatewaySecret = "test-secret";

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Gateway-Secret", GatewaySecret);
        return client;
    }

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithReuse(true)
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3-alpine")
        .WithReuse(true)
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:consolidation-db", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:messaging", _rabbitmq.GetConnectionString());
        builder.UseSetting("Gateway:Secret", GatewaySecret);
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.AddMassTransitTestHarness();
        });
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        // Clean state from previous runs when container is reused
        try
        {
            await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
            await conn.OpenAsync();
            var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public", "consolidation"],
                TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
            });
            await respawner.ResetAsync(conn);
        }
        catch (InvalidOperationException)
        {
            // First run — DB has no tables yet (migrations run at app startup)
        }
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
        await base.DisposeAsync();
    }
}
