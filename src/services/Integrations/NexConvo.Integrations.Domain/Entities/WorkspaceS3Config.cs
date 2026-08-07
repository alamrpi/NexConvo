using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Domain.Health;

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

    public DateTimeOffset? LastTestedAt { get; private set; }
    public ConnectionStatus LastTestStatus { get; private set; } = ConnectionStatus.Untested;
    public string? LastTestError { get; private set; }
    public int? LastTestLatencyMs { get; private set; }

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

    public void ApplyHealth(ConnectionHealth health)
    {
        LastTestStatus = health.Status;
        LastTestError = health.ErrorMessage;
        LastTestLatencyMs = health.LatencyMs;
        LastTestedAt = DateTimeOffset.UtcNow;
    }

    public void EnsureHealthy()
    {
        if (LastTestStatus != ConnectionStatus.Healthy)
            throw new ConnectionUnhealthyException("s3", LastTestError ?? "Run a connection test in Settings.");
    }
}
