using CashFlow.Identity.API;
using CashFlow.Identity.API.Auth;
using CashFlow.Identity.API.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<IdentityDbContext>("identity-db");

builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("identity-db", tags: ["ready"]);

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<IdentityDbContext>();

builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"]!;
    var issuer = "cashflow-identity";
    var audience = builder.Configuration["Identity:Audience"] ?? "cashflow-api";

    var protector = new JwtTokenProtector(signingKey, issuer, audience);
    options.BearerTokenProtector = protector;
    options.RefreshTokenProtector = protector;
    options.BearerTokenExpiration = TimeSpan.FromHours(1);
    options.RefreshTokenExpiration = TimeSpan.FromDays(7);
});

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredUniqueChars = 4;
});

builder.Services.AddScoped<IUserClaimsPrincipalFactory<IdentityUser>, AudienceClaimsPrincipalFactory>();

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseProductionHttpsSecurity();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

app.MapGroup("/api/identity")
    .MapIdentityApi<IdentityUser>();

app.Run();

public partial class Program;
