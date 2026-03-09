using System.Text;
using CashFlow.Identity.API.Auth;
using CashFlow.Identity.API.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;

namespace CashFlow.Identity.API.Extensions;

internal static class IdentityExtensions
{
    internal static WebApplicationBuilder AddIdentity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        builder.Services.AddSingleton<JwtTokenProtector>(sp =>
        {
            var signingKey = builder.Configuration["Jwt:SigningKey"]
                             ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required");
            if (Encoding.UTF8.GetByteCount(signingKey) < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");
            }

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

        return builder;
    }
}
