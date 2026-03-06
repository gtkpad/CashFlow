var builder = DistributedApplication.CreateBuilder(args);

// Dev: dotnet user-secrets set "Parameters:gateway-secret" "<value>" --project src/CashFlow.AppHost
// Produção: Azure Key Vault ou variável de ambiente
var gatewaySecret = builder.AddParameter("gateway-secret", secret: true);
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

// Application Insights (producao): APPLICATIONINSIGHTS_CONNECTION_STRING
// configurada via azd env set ou Azure Portal. Em dev local, Aspire Dashboard recebe telemetria via OTLP.
var serviceVersion = builder.Configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0-dev";

// Skip dev-only resources in E2E tests (set by CashFlowAppFixture before CreateAsync).
// Deve ser verificado ANTES de AddAzureContainerAppEnvironment para evitar que recursos
// Azure implícitos bloqueiem o StartAsync em CI (onde não há conectividade Azure).
var skipDevResources = Environment.GetEnvironmentVariable("CASHFLOW_E2E_TESTING") == "true";

// Infrastructure
// Em produção: Azure Container Apps + Azure Database for PostgreSQL Flexible Server
// Em dev local e E2E: containers locais (RunAsContainer)
// AddAzureContainerAppEnvironment configura o target de deployment para Azure Container Apps.
// Em E2E Testing e dev local, este recurso é desnecessário e pode bloquear o startup.
if (!skipDevResources)
{
    builder.AddAzureContainerAppEnvironment("env");
}

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(c =>
    {
        // Volumes persistem a senha do PostgreSQL entre runs — incompatível com o Aspire Testing,
        // que gera senhas aleatórias. Omitir em Testing/CI para evitar "password authentication failed".
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

// Volumes e management UI são recursos de desenvolvimento — omitidos em Testing/CI
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

// Em dev local, usar WithHttpHealthCheck para que o DCP monitore readiness
// e o Aspire Dashboard mostre o estado correto de cada serviço.
// Em E2E/CI, omitir health check probes — o DCP health probing nunca transiciona
// os serviços com MassTransit de Running para Healthy, bloqueando StartAsync.
if (!skipDevResources)
{
    identity.WithHttpHealthCheck("/health");
    transactions.WithHttpHealthCheck("/health");
    consolidation.WithHttpHealthCheck("/health");
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

if (skipDevResources)
{
    // E2E/CI: esperar apenas Running (WaitForStart) em vez de Healthy (WaitFor).
    // Sem WithHttpHealthCheck nos serviços, WaitFor bloquearia indefinidamente.
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
