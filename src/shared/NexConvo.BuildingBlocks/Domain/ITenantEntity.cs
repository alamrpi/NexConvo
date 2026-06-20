namespace NexConvo.BuildingBlocks.Domain;

/// <summary>Marks an entity as tenant-scoped. RLS + the EF query filter key off this.</summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
