using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Domain.Entities;

public class WorkspaceAiConfig : BaseAggregateRoot
{
    public AiProviderType Provider { get; private set; }
    public string EncryptedApiKey { get; private set; }
    public string? BaseUrl { get; private set; }
    public string DefaultModel { get; private set; }
    public string? SystemPrompt { get; private set; }
    public string? Parameters { get; private set; } // JSONB stored as string
    public bool IsActive { get; private set; }

    public DateTimeOffset? LastTestedAt { get; private set; }
    public ConnectionStatus LastTestStatus { get; private set; } = ConnectionStatus.Untested;
    public string? LastTestError { get; private set; }
    public int? LastTestLatencyMs { get; private set; }

    private WorkspaceAiConfig()
    {
        // EF Core
        EncryptedApiKey = null!;
        DefaultModel = null!;
    }

    public WorkspaceAiConfig(
        Guid tenantId,
        AiProviderType provider,
        string encryptedApiKey,
        string? baseUrl,
        string defaultModel,
        string? systemPrompt,
        string? parameters,
        bool isActive)
    {
        TenantId = tenantId;
        Provider = provider;
        EncryptedApiKey = encryptedApiKey;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        SystemPrompt = systemPrompt;
        Parameters = parameters;
        IsActive = isActive;
    }

    public void UpdateSettings(
        string encryptedApiKey,
        string? baseUrl,
        string defaultModel,
        string? systemPrompt,
        string? parameters)
    {
        EncryptedApiKey = encryptedApiKey;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        SystemPrompt = systemPrompt;
        Parameters = parameters;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

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
            throw new ConnectionUnhealthyException("ai", LastTestError ?? "Run a connection test in Settings.");
    }
}
