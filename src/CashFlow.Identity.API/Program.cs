using CashFlow.Identity.API;
using CashFlow.Identity.API.Auth;
using CashFlow.Identity.API.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureNpgsqlDbContext<IdentityDbContext>("identity-db");

builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("identity-db", tags: ["ready"]);

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<IdentityDbContext>();

builder.Services.AddSingleton<JwtTokenProtector>(sp =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"]
        ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required");
    if (System.Text.Encoding.UTF8.GetByteCount(signingKey) < 32)
        throw new InvalidOperationException(
            "Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");
    var logger = sp.GetRequiredService<ILogger<JwtTokenProtector>>();
    return new JwtTokenProtector(signingKey, "cashflow-identity",
        builder.Configuration["Identity:Audience"] ?? "cashflow-api", logger);
});

builder.Services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)
    .Configure<JwtTokenProtector>((options, protector) =>
    {
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseGlobalExceptionHandling();
app.MapDefaultEndpoints();
app.UseMiddleware<CashFlow.ServiceDefaults.GatewaySecretMiddleware>();

// DisableAntiforgery: Identity endpoints are stateless (JWT Bearer). CSRF não se aplica a JSON/Bearer auth.
app.MapGroup("/api/identity")
    .MapIdentityApi<IdentityUser>()
    .DisableAntiforgery();

app.Run();

public partial class Program;
