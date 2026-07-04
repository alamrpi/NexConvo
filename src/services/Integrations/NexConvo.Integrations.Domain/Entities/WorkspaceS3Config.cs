using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Integrations.Domain.Entities;

public class WorkspaceS3Config : BaseAggregateRoot
{
    public string BucketName { get; private set; }
    public string Region { get; private set; }
    public string EncryptedAccessKeyId { get; private set; }
    public string EncryptedSecretAccessKey { get; private set; }

    /// <summary>Optional custom endpoint for S3-compatible providers (Cloudflare R2, MinIO, DigitalOcean Spaces).</summary>
    public string? CustomEndpoint { get; private set; }

    /// <summary>Optional path prefix applied to all object keys, e.g. "knowledge/".</summary>
    public string? PathPrefix { get; private set; }

    public bool IsActive { get; private set; }

    private WorkspaceS3Config()
    {
        BucketName = null!;
        Region = null!;
        EncryptedAccessKeyId = null!;
        EncryptedSecretAccessKey = null!;
    }

    public WorkspaceS3Config(
        Guid tenantId,
        string bucketName,
        string region,
        string encryptedAccessKeyId,
        string encryptedSecretAccessKey,
        string? customEndpoint,
        string? pathPrefix,
        bool isActive)
    {
        TenantId = tenantId;
        BucketName = bucketName;
        Region = region;
        EncryptedAccessKeyId = encryptedAccessKeyId;
        EncryptedSecretAccessKey = encryptedSecretAccessKey;
        CustomEndpoint = customEndpoint;
        PathPrefix = pathPrefix;
        IsActive = isActive;
    }

    public void UpdateSettings(
        string bucketName,
        string region,
        string encryptedAccessKeyId,
        string encryptedSecretAccessKey,
        string? customEndpoint,
        string? pathPrefix)
    {
        BucketName = bucketName;
        Region = region;
        EncryptedAccessKeyId = encryptedAccessKeyId;
        EncryptedSecretAccessKey = encryptedSecretAccessKey;
        CustomEndpoint = customEndpoint;
        PathPrefix = pathPrefix;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
