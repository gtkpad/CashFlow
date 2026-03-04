using CashFlow.Identity.API.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<IdentityDbContext>("identity-db");

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<IdentityDbContext>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-apply migrations on startup (dev only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

app.MapGroup("/api/identity")
    .MapIdentityApi<IdentityUser>();

app.Run();

public partial class Program;
