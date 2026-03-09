using Carter;
using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using CashFlow.Transactions.API.Features.CreateTransaction;
using CashFlow.Transactions.API.Features.GetTransaction;
using CashFlow.Transactions.API.Persistence;
using FluentValidation;
using MassTransit;

namespace CashFlow.Transactions.API.Extensions;

internal static class ApplicationExtensions
{
    internal static WebApplicationBuilder AddApplication(this WebApplicationBuilder builder)
    {
        builder.Services.AddCarter(configurator: c =>
        {
            c.WithModule<CreateTransactionEndpoint>();
            c.WithModule<GetTransactionEndpoint>();
        });

        builder.Services.AddValidatorsFromAssemblyContaining<CreateTransactionValidator>();
        builder.Services.AddScoped<DomainEventInterceptor>(sp =>
            new DomainEventInterceptor(() => sp.GetRequiredService<IPublishEndpoint>()));
        builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
        builder.Services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<TransactionsDbContext>());
        builder.Services.AddScoped<CreateTransactionHandler>();
        builder.Services.AddScoped<GetTransactionHandler>();

        builder.Services.AddOpenApi();

        return builder;
    }
}
