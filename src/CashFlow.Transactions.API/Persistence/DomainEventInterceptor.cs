using CashFlow.Domain.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CashFlow.Transactions.API.Persistence;

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
