var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("env");

// Dev: dotnet user-secrets set "Parameters:gateway-secret" "<value>" --project src/CashFlow.AppHost
// Produção: Azure Key Vault ou variável de ambiente
var gatewaySecret = builder.AddParameter("gateway-secret", secret: true);
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

// Application Insights (producao): APPLICATIONINSIGHTS_CONNECTION_STRING
// configurada via azd env set ou Azure Portal. Em dev local, Aspire Dashboard recebe telemetria via OTLP.

// Infrastructure
// Em produção: Azure Database for PostgreSQL Flexible Server
// Em dev local: container PostgreSQL (RunAsContainer)
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(c => c.WithDataVolume().WithPgAdmin());

var identityDb = postgres.AddDatabase("identity-db");
var transactionsDb = postgres.AddDatabase("transactions-db");
var consolidationDb = postgres.AddDatabase("consolidation-db");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin();

// Services
var identity = builder.AddProject<Projects.CashFlow_Identity_API>("identity")
    .WithReference(identityDb)
    .WithEnvironment("Identity__Audience", "cashflow-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("OTEL_SERVICE_VERSION", "1.0.0")
    .WithHttpHealthCheck("/health")
    .WaitFor(identityDb);

var transactions = builder.AddProject<Projects.CashFlow_Transactions_API>("transactions")
    .WithReference(transactionsDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WithEnvironment("OTEL_SERVICE_VERSION", "1.0.0")
    .WithHttpHealthCheck("/health")
    .WaitFor(transactionsDb)
    .WaitFor(rabbitmq);

var consolidation = builder.AddProject<Projects.CashFlow_Consolidation_API>("consolidation")
    .WithReference(consolidationDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WithEnvironment("OTEL_SERVICE_VERSION", "1.0.0")
    .WithHttpHealthCheck("/health")
    .WaitFor(consolidationDb)
    .WaitFor(rabbitmq);

// Gateway
builder.AddProject<Projects.CashFlow_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(transactions)
    .WithReference(consolidation)
    .WithEnvironment("Identity__ValidAudiences__0", "cashflow-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WithEnvironment("OTEL_SERVICE_VERSION", "1.0.0")
    .WithHttpHealthCheck("/health")
    .WaitFor(identity)
    .WaitFor(transactions)
    .WaitFor(consolidation)
    .WithExternalHttpEndpoints();

builder.Build().Run();
