using CashFlow.Transactions.API.Persistence;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CashFlow.Transactions.API.Extensions;

internal static class MessagingExtensions
{
    internal static WebApplicationBuilder AddMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<TransactionsDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromMilliseconds(100);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
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
