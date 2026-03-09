using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Consolidation.API.Persistence;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CashFlow.Consolidation.API.Extensions;

internal static class MessagingExtensions
{
    internal static WebApplicationBuilder AddMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<TransactionCreatedConsumer, TransactionCreatedConsumerDefinition>();
            x.AddConsumer<TransactionFaultConsumer>();

            x.AddEntityFrameworkOutbox<ConsolidationDbContext>(o =>
            {
                o.UsePostgres();
            });

            x.ConfigureHealthCheckOptions(options =>
            {
                options.Name = "rabbitmq";
                options.MinimalFailureStatus = HealthStatus.Unhealthy;
                options.Tags.Add("ready");
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("messaging"));
                cfg.ConfigureEndpoints(context);
            });
        });

        return builder;
    }
}
