using CashFlow.Identity.API.Extensions;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDatabase();
builder.AddIdentity();

var app = builder.Build();
app.UseProductionHttpsSecurity();
await app.MigrateDatabaseAsync();
app.UseGlobalExceptionHandling();
app.MapDefaultEndpoints();
app.UseGatewaySecret();

// DisableAntiforgery: Identity endpoints are stateless (JWT Bearer). CSRF não se aplica a JSON/Bearer auth.
app.MapGroup("/api/identity")
    .MapIdentityApi<IdentityUser>()
    .DisableAntiforgery();

app.Run();

public partial class Program;
