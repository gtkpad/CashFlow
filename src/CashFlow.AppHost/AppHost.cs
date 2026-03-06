var builder = DistributedApplication.CreateBuilder(args);

var gatewaySecret = builder.AddParameter("gateway-secret", secret: true);
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

var serviceVersion = builder.Configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0-dev";
var skipDevResources = Environment.GetEnvironmentVariable("CASHFLOW_E2E_TESTING") == "true";

// Application Insights: só registrar o resource quando a connection string existir.
// Em E2E/CI a variável não existe — um resource sem valor falha e cascata para todos os serviços.
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var appInsights = !string.IsNullOrEmpty(appInsightsConnectionString)
    ? builder.AddConnectionString("appinsights", "APPLICATIONINSIGHTS_CONNECTION_STRING")
    : null;

// Infrastructure
if (!skipDevResources)
{
    builder.AddAzureContainerAppEnvironment("env");
}

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(c =>
    {
        if (!skipDevResources)
        {
            c.WithDataVolume();
            c.WithPgAdmin();
        }
    });

var identityDb = postgres.AddDatabase("identity-db");
var transactionsDb = postgres.AddDatabase("transactions-db");
var consolidationDb = postgres.AddDatabase("consolidation-db");

var rabbitmq = builder.AddRabbitMQ("messaging");

if (!skipDevResources)
{
    rabbitmq.WithDataVolume();
    rabbitmq.WithManagementPlugin();
}

// Services
var identity = builder.AddProject<Projects.CashFlow_Identity_API>("identity")
    .WithReference(identityDb)
    .WithEnvironment("Identity__Audience", "cashflow-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("OTEL_SERVICE_VERSION", serviceVersion)
    .WaitFor(identityDb);

var transactions = builder.AddProject<Projects.CashFlow_Transactions_API>("transactions")
    .WithReference(transactionsDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WithEnvironment("OTEL_SERVICE_VERSION", serviceVersion)
    .WaitFor(transactionsDb)
    .WaitFor(rabbitmq);

var consolidation = builder.AddProject<Projects.CashFlow_Consolidation_API>("consolidation")
    .WithReference(consolidationDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WithEnvironment("OTEL_SERVICE_VERSION", serviceVersion)
    .WaitFor(consolidationDb)
    .WaitFor(rabbitmq);

if (!skipDevResources)
{
    identity.WithHttpHealthCheck("/health");
    transactions.WithHttpHealthCheck("/health");
    consolidation.WithHttpHealthCheck("/health");
}

if (appInsights is not null)
{
    identity.WithReference(appInsights);
    transactions.WithReference(appInsights);
    consolidation.WithReference(appInsights);
}

// Gateway
var gateway = builder.AddProject<Projects.CashFlow_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(transactions)
    .WithReference(consolidation)
    .WithEnvironment("Identity__ValidAudiences__0", "cashflow-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WithEnvironment("OTEL_SERVICE_VERSION", serviceVersion)
    .WithExternalHttpEndpoints();

if (appInsights is not null)
{
    gateway.WithReference(appInsights);
}

if (skipDevResources)
{
    gateway.WaitForStart(identity);
    gateway.WaitForStart(transactions);
    gateway.WaitForStart(consolidation);
}
else
{
    gateway.WithHttpHealthCheck("/health");
    gateway.WaitFor(identity);
    gateway.WaitFor(transactions);
    gateway.WaitFor(consolidation);
}

builder.Build().Run();
