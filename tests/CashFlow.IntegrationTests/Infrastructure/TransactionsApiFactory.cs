using CashFlow.Transactions.API.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace CashFlow.IntegrationTests.Infrastructure;

// Uses TransactionsDbContext as anchor type since both APIs define
// a global 'Program' class, causing ambiguity at compile time.
public class TransactionsApiFactory : WebApplicationFactory<TransactionsDbContext>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3-management-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:transactions-db", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:messaging", _rabbitmq.GetConnectionString());

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitmq.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
        await base.DisposeAsync();
    }
}
