namespace NexConvo.Chat.Application.Features.Health;

/// <summary>
/// Background "health sweep" (Standard 22d): re-tests every tenant's active
/// <see cref="NexConvo.Chat.Domain.Entities.ChannelConnection"/>s on the owner (RLS-free)
/// connection, persists the result via <c>ApplyHealth</c>, and publishes
/// <c>IntegrationHealthFailedEvent</c> on a Healthy→Failed transition only (dedupe — never on
/// Failed→Failed). Triggered by
/// <see cref="NexConvo.Contracts.Messages.CheckIntegrationHealthCommand"/> fanned out by the
/// Automation scheduler.
/// </summary>
public interface IChatHealthSweepService
{
    Task RunAsync(CancellationToken ct);
}
