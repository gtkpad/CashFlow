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

builder.AddNpgsqlDbContext<TransactionsDbContext>("transactions-db");

builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<TransactionsDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromMilliseconds(100);
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
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
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<CreateTransactionHandler>();
builder.Services.AddScoped<GetTransactionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-apply migrations on startup (dev only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.UseMiddleware<CashFlow.ServiceDefaults.GatewaySecretMiddleware>();

var v1 = app.MapGroup("api/v1");
v1.MapCarter();

app.Run();

public partial class Program;
