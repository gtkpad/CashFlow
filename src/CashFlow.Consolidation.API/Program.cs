using Carter;
using CashFlow.Consolidation.API.Features.GetDailyBalance;
using CashFlow.Consolidation.API.Features.TransactionCreated;
using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.Consolidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<ConsolidationDbContext>("consolidation-db");

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConsolidationDbContext>("consolidation-db", tags: ["ready"]);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionCreatedConsumer, TransactionCreatedConsumerDefinition>();

    x.AddEntityFrameworkOutbox<ConsolidationDbContext>(o =>
    {
        o.UsePostgres();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("messaging"));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddCarter(configurator: c =>
{
    c.WithModule<GetDailyBalanceEndpoint>();
});
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("DailyBalance",
        new DailyBalanceCachePolicy());
});
builder.Services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
builder.Services.AddScoped<GetDailyBalanceHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseProductionHttpsSecurity();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();
app.UseGlobalExceptionHandling();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.UseMiddleware<CashFlow.ServiceDefaults.GatewaySecretMiddleware>();
app.UseOutputCache();

var v1 = app.MapGroup("api/v1");
v1.MapCarter();

app.Run();

public partial class Program;
