using Asp.Versioning;
using Carter;
using CashFlow.Consolidation.API.Features.GetDailyBalance;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;

namespace CashFlow.Consolidation.API.Extensions;

internal static class ApplicationExtensions
{
    internal static WebApplicationBuilder AddApplication(this WebApplicationBuilder builder)
    {
        builder.Services.AddCarter(configurator: c =>
        {
            c.WithModule<GetDailyBalanceEndpoint>();
        });

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddOutputCache(options =>
        {
            options.AddPolicy("DailyBalance",
                new DailyBalanceCachePolicy(TimeProvider.System));
        });

        builder.Services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        builder.Services.AddScoped<GetDailyBalanceHandler>();

        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });
        builder.Services.AddOpenApi();

        return builder;
    }
}
