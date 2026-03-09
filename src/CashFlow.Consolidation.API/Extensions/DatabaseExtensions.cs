using CashFlow.Consolidation.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Extensions;

internal static class DatabaseExtensions
{
    internal static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.AddAzureNpgsqlDbContext<ConsolidationDbContext>("consolidation-db",
            configureDbContextOptions: options =>
            {
                options.UseNpgsql(npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3));
            });

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<ConsolidationDbContext>("consolidation-db", tags: ["ready"]);

        return builder;
    }

    internal static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        // MigrateAsync is idempotent and uses pg_advisory_lock to serialize
        // concurrent migrations across replicas — safe for production startup.
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        await db.Database.MigrateAsync();
    }
}
