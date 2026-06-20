namespace NexConvo.BuildingBlocks.Domain;

/// <summary>Audit stamps applied automatically on save (skill Standard 14 supports this).</summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    Guid CreatedByUserId { get; set; }
}
