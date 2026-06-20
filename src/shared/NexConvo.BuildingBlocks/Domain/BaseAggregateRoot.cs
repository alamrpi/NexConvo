namespace NexConvo.BuildingBlocks.Domain;

/// <summary>
/// Base class for aggregate roots. Owns identity, tenant + audit stamps, and a
/// collection of domain events that the persistence layer drains after commit.
/// </summary>
public abstract class BaseAggregateRoot : ITenantEntity, IAuditableEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
