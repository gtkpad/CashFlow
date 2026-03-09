using CashFlow.Domain.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CashFlow.Transactions.API.Persistence;

/// <summary>
///     EF Core interceptor que publica domain events como integration events
///     via MassTransit antes de a transação SaveChanges ser confirmada.
/// </summary>
/// <remarks>
///     Recebe uma <see cref="Func{IPublishEndpoint}"/> factory em vez de um
///     <see cref="IPublishEndpoint"/> direto para quebrar a dependência circular de DI:
///     tanto <see cref="TransactionsDbContext"/> (Scoped) quanto <see cref="IPublishEndpoint"/>
///     (Scoped via MassTransit) são registrados no mesmo scope, causando deadlock de
///     inicialização se o endpoint for resolvido no construtor do DbContext.
///     A factory adia a resolução para o primeiro <c>SavingChangesAsync</c>, após
///     o grafo de dependências estar completamente construído.
///     Ver <see cref="ApplicationExtensions"/> para o padrão de registro.
/// </remarks>
public sealed class DomainEventInterceptor(Func<IPublishEndpoint> publishEndpointFactory) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entities = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        var publishEndpoint = publishEndpointFactory();

        foreach (var domainEvent in domainEvents)
        {
            await DomainEventMapper.PublishIntegrationEvent(
                domainEvent, publishEndpoint, cancellationToken);
        }

        foreach (var entity in entities)
            entity.ClearDomainEvents();

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
