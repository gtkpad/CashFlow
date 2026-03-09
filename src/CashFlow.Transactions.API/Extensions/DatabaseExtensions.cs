using CashFlow.Transactions.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Extensions;

internal static class DatabaseExtensions
{
    internal static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.AddAzureNpgsqlDbContext<TransactionsDbContext>("transactions-db",
            configureDbContextOptions: options =>
            {
                options.UseNpgsql(npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(3));
            });

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
