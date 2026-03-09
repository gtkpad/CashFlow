using Azure.Monitor.OpenTelemetry.AspNetCore;
using CashFlow.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.Configure<ServiceProviderOptions>(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            });
        }

        builder.ConfigureOpenTelemetry();
        builder.AddProductionLogFilters();

        builder.AddDefaultHealthChecks();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton<CashFlowMetrics>();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("MassTransit")
                    .AddMeter(CashFlowMetrics.MeterName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("MassTransit")
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        // Aspire WithReference injeta como ConnectionStrings__appinsights;
        // deploy direto (ou legacy) pode usar APPLICATIONINSIGHTS_CONNECTION_STRING.
        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                                          ?? builder.Configuration.GetConnectionString("appinsights");

        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            var samplingRatio = double.TryParse(
                builder.Configuration["OTEL_TRACES_SAMPLER_ARG"], out var ratio)
                ? (float)ratio
                : builder.Environment.IsDevelopment()
                    ? 1.0f
                    : 0.1f;

            builder.Services.AddOpenTelemetry()
                .UseAzureMonitor(o =>
                {
                    o.ConnectionString = appInsightsConnectionString;
                    o.SamplingRatio = samplingRatio;
                });
        }

        return builder;
    }

    private static TBuilder AddProductionLogFilters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    public static WebApplication UseGlobalExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }

    public static WebApplication UseProductionHttpsSecurity(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        return app;
    }

    public static WebApplication UseGatewaySecret(this WebApplication app)
    {
        app.UseMiddleware<GatewaySecretMiddleware>();
        return app;
    }
}
