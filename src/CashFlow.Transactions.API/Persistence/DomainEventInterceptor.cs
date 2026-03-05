using CashFlow.Domain.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CashFlow.Transactions.API.Persistence;

public sealed class DomainEventInterceptor(IPublishEndpoint publishEndpoint) : SaveChangesInterceptor
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

        foreach (var domainEvent in domainEvents)
        {
            var integrationEvent = DomainEventMapper.Map(domainEvent);
            var eventType = DomainEventMapper.GetIntegrationEventType(domainEvent);

            if (integrationEvent is not null && eventType is not null)
                await publishEndpoint.Publish(integrationEvent, eventType, cancellationToken);
        }

        foreach (var entity in entities)
            entity.ClearDomainEvents();

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
