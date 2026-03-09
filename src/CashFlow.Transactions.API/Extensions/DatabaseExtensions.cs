using CashFlow.Transactions.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Extensions;

internal static class DatabaseExtensions
{
    internal static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        // Note: Uses Aspire.Npgsql (non-Azure) because TransactionsDbContext injects a scoped
        // DomainEventInterceptor via OnConfiguring, which is incompatible with DbContext pooling
        // enabled by AddAzureNpgsqlDbContext. Consolidation API can use Azure variant because
        // it has no interceptor dependency.
        builder.Services.AddDbContext<TransactionsDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("transactions-db"),
                npgsql => npgsql.EnableRetryOnFailure(3)));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TransactionsDbContext>("transactions-db", tags: ["ready"]);

        return builder;
    }

    internal static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        // MigrateAsync is idempotent and uses pg_advisory_lock to serialize
        // concurrent migrations across replicas — safe for production startup.
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await db.Database.MigrateAsync();
    }
}
