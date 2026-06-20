namespace NexConvo.Contracts.Events.Voice;

/// <summary>
/// Published by Voice Orchestration when a call ends and its transcript is ready.
/// Consumed by Core CRM to run NLU entity extraction and auto-advance the pipeline.
/// </summary>
public sealed record CallCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid CallId { get; init; }
    public required Guid TenantId { get; init; }
    public Guid? LeadId { get; init; }

    /// <summary>E.164 of the other party.</summary>
    public required string CounterpartyE164 { get; init; }

    public required int DurationSeconds { get; init; }

    /// <summary>Reference to the stored transcript (not the transcript text itself).</summary>
    public required string TranscriptRef { get; init; }
}
