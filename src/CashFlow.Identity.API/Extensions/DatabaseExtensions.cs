using CashFlow.Identity.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Identity.API.Extensions;

internal static class DatabaseExtensions
{
    internal static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.AddAzureNpgsqlDbContext<IdentityDbContext>("identity-db");

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<IdentityDbContext>("identity-db", tags: ["ready"]);

        return builder;
    }

    internal static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }
}
