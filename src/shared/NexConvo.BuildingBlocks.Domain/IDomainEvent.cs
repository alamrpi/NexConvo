using MediatR;

namespace NexConvo.BuildingBlocks.Domain;

/// <summary>
/// In-process domain event raised by an aggregate. Translated to an outbound
/// <c>IntegrationEvent</c> after commit (skill Standard 10). Extends MediatR's
/// <see cref="INotification"/> so handlers can subscribe.
/// </summary>
public interface IDomainEvent : INotification
{
}
