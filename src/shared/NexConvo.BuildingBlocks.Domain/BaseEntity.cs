namespace NexConvo.BuildingBlocks.Domain;

/// <summary>
/// Base class for aggregate roots that are NOT tenant-scoped (e.g. the Tenant registry root).
/// Owns identity, audit stamps, and a domain-event collection drained after commit — but no
/// <see cref="ITenantEntity.TenantId"/>, so it carries no RLS policy. Tenant-scoped aggregates
/// use <see cref="BaseAggregateRoot"/> instead.
/// </summary>
public abstract class BaseEntity : IAuditableEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
