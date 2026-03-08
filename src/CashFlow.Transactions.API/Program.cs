using Carter;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Features;
using CashFlow.Transactions.API.Features.CreateTransaction;
using CashFlow.Transactions.API.Features.GetTransaction;
using CashFlow.Transactions.API.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<TransactionsDbContext>((sp, options) =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("transactions-db"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3));
    options.AddInterceptors(sp.GetRequiredService<DomainEventInterceptor>());
});
builder.EnrichAzureNpgsqlDbContext<TransactionsDbContext>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TransactionsDbContext>("transactions-db", tags: ["ready"]);

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
        options.MinimalFailureStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy;
        options.Tags.Add("ready");
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("messaging"));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddCarter(configurator: c =>
{
    c.WithModule<TransactionEndpoints>();
    c.WithValidator<CreateTransactionValidator>();
});
builder.Services.AddValidatorsFromAssemblyContaining<CreateTransactionValidator>();
builder.Services.AddScoped<DomainEventInterceptor>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<CashFlow.Domain.SharedKernel.IUnitOfWork>(sp =>
    sp.GetRequiredService<TransactionsDbContext>());
builder.Services.AddScoped<CreateTransactionHandler>();
builder.Services.AddScoped<GetTransactionHandler>();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseProductionHttpsSecurity();

// EF Core MigrateAsync is idempotent and uses pg_advisory_lock to serialize
// concurrent migrations across replicas — safe for production startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();
app.UseGlobalExceptionHandling();
app.MapOpenApi();
app.UseMiddleware<CashFlow.ServiceDefaults.GatewaySecretMiddleware>();

var v1 = app.MapGroup("api/v1");
v1.MapCarter();

app.Run();

public partial class Program;
