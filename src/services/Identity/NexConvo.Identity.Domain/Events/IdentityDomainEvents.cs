using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.Events;

public sealed record TenantProvisionedDomainEvent(Guid TenantId, string Name, string Slug) : IDomainEvent;

public sealed record UserRegisteredDomainEvent(Guid UserId, Guid TenantId, string Email, string FullName)
    : IDomainEvent;
