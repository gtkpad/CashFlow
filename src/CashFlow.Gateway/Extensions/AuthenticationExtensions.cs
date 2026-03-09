using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Gateway.Extensions;

internal static class AuthenticationExtensions
{
    internal static WebApplicationBuilder AddGatewayAuthentication(
        this WebApplicationBuilder builder)
    {
        var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
                            ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required");
        if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");
        }

        var validAudiences = builder.Configuration
            .GetSection("Identity:ValidAudiences").Get<string[]>() ?? ["cashflow-api"];

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = "cashflow-identity",
                    ValidateAudience = validAudiences is { Length: > 0 },
                    ValidAudiences = validAudiences,
                    ValidateLifetime = true
                };
            });

        builder.Services.AddAuthorization(options =>
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return builder;
    }
}
