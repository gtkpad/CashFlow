namespace CashFlow.Domain.SharedKernel;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected init; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}