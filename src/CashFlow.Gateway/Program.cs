using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using CashFlow.Gateway.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var validAudiences = builder.Configuration.GetSection("Identity:ValidAudiences").Get<string[]>()
    ?? ["cashflow-api"];

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required");
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = "cashflow-identity",
            ValidateAudience = validAudiences is { Length: > 0 },
            ValidAudiences = validAudiences,
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayPolicy", policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173");
        }
        // Production without config: no origins allowed (default deny)

        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithHeaders("Authorization", "Content-Type", "Accept", "X-Trace-Id")
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS");
        }

        policy.AllowCredentials();
    });
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Accept forwarded headers from any proxy (Azure Container Apps uses RFC 1918 ranges)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3600,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

app.UseProductionHttpsSecurity();
app.UseGlobalExceptionHandling();
app.MapDefaultEndpoints();

app.UseForwardedHeaders();
app.UseResponseCompression();

// Security headers + trace ID correlation
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=()";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

    var traceId = Activity.Current?.TraceId.ToString();
    if (traceId is not null)
        headers["X-Trace-Id"] = traceId;

    await next();
});

app.UseRateLimiter();
app.UseCors("GatewayPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthMiddleware>();

app.MapReverseProxy();

app.Run();

public partial class Program;
