using Carter;
using CashFlow.Transactions.API.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDatabase();
builder.AddMessaging();
builder.AddApplication();

var app = builder.Build();
app.UseProductionHttpsSecurity();
await app.MigrateDatabaseAsync();
app.UseGlobalExceptionHandling();
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.UseGatewaySecret();
app.MapGroup("api/v1").MapCarter();

app.Run();

public partial class Program;
