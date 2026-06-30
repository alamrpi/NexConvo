using NexConvo.BuildingBlocks.Domain;
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
}
