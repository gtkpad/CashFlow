using CashFlow.Gateway.Extensions;
using CashFlow.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
builder.AddGatewayAuthentication();
builder.AddGatewayCors();
builder.AddGatewayNetwork();

var app = builder.Build();
app.UseProductionHttpsSecurity();
app.UseGlobalExceptionHandling();
app.MapDefaultEndpoints();
app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseCors("GatewayPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthMiddleware>();
app.MapReverseProxy();

app.Run();

public partial class Program;
