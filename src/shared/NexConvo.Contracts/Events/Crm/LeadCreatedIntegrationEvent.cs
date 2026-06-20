using NexConvo.Contracts.Enums;

namespace NexConvo.Contracts.Events.Crm;

/// <summary>
/// Published by Core CRM after a lead is created. Consumed by Chat (auto-reply) and
/// AI Command (enrichment). Carries only identifiers + the minimum contact fields.
/// </summary>
public sealed record LeadCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid LeadId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ContactName { get; init; }
    public string? Email { get; init; }

    /// <summary>E.164 normalized, e.g. +8801712345678.</summary>
    public string? PhoneE164 { get; init; }

    public required LeadSourceChannel SourceChannel { get; init; }
}
