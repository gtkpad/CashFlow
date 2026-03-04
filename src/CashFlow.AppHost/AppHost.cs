var builder = DistributedApplication.CreateBuilder(args);

var gatewaySecret = builder.AddParameter("gateway-secret", secret: true);

// Infrastructure
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var identityDb = postgres.AddDatabase("identity-db");
var transactionsDb = postgres.AddDatabase("transactions-db");
var consolidationDb = postgres.AddDatabase("consolidation-db");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

// Services
var identity = builder.AddProject<Projects.CashFlow_Identity_API>("identity")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var transactions = builder.AddProject<Projects.CashFlow_Transactions_API>("transactions")
    .WithReference(transactionsDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WaitFor(transactionsDb)
    .WaitFor(rabbitmq);

var consolidation = builder.AddProject<Projects.CashFlow_Consolidation_API>("consolidation")
    .WithReference(consolidationDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WaitFor(consolidationDb)
    .WaitFor(rabbitmq);

// Gateway
builder.AddProject<Projects.CashFlow_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(transactions)
    .WithReference(consolidation)
    .WithEnvironment("Identity__Authority", identity.GetEndpoint("http"))
    .WithEnvironment("Gateway__Secret", gatewaySecret)
    .WaitFor(identity)
    .WaitFor(transactions)
    .WaitFor(consolidation)
    .WithExternalHttpEndpoints();

builder.Build().Run();
