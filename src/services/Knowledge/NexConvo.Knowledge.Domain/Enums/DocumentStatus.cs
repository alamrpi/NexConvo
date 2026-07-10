namespace NexConvo.Knowledge.Domain.Enums;

/// <summary>Processing status of a KnowledgeDocument in the ingestion pipeline.</summary>
public enum DocumentStatus
{
    Pending = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3,
}
