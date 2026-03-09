namespace CashFlow.Gateway.Extensions;

internal static class CorsExtensions
{
    internal static WebApplicationBuilder AddGatewayCors(this WebApplicationBuilder builder)
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("GatewayPolicy", policy =>
            {
                if (allowedOrigins is { Length: > 0 })
                    policy.WithOrigins(allowedOrigins);
                else if (builder.Environment.IsDevelopment())
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173");

                if (builder.Environment.IsDevelopment())
                    policy.AllowAnyHeader().AllowAnyMethod();
                else
                {
                    policy.WithHeaders("Authorization", "Content-Type", "Accept", "X-Trace-Id")
                        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS");
                }

                policy.AllowCredentials();
            });
        });

        return builder;
    }
}
