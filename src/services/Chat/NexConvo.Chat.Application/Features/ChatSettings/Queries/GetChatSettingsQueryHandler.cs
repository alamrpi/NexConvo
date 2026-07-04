using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.ChatSettings.Dtos;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;
using System.Text.Json;

namespace NexConvo.Chat.Application.Features.ChatSettings.Queries;

public sealed class GetChatSettingsQueryHandler(
    IChatDbContext db,
    ITenantContext tenant,
    ILogger<GetChatSettingsQueryHandler> logger)
    : IRequestHandler<GetChatSettingsQuery, WorkspaceChatSettingsDto>
{
    public async Task<WorkspaceChatSettingsDto> Handle(GetChatSettingsQuery request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var settings = await db.WorkspaceChatSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (settings is null)
        {
            logger.LogInformation("No chat settings found for tenant {TenantId}, returning defaults", tenantId);
            return Defaults();
        }

        var fallback = string.IsNullOrEmpty(settings.FallbackProviders) || settings.FallbackProviders == "[]"
            ? Array.Empty<AiProviderType>()
            : JsonSerializer.Deserialize<List<string>>(settings.FallbackProviders)!
                .Select(s => Enum.Parse<AiProviderType>(s, ignoreCase: true))
                .ToArray();

        var phrases = string.IsNullOrEmpty(settings.TriggerPhrases) || settings.TriggerPhrases == "[]"
            ? Array.Empty<string>()
            : (JsonSerializer.Deserialize<string[]>(settings.TriggerPhrases) ?? Array.Empty<string>());

        return new WorkspaceChatSettingsDto(
            settings.Id,
            settings.PrimaryProvider,
            settings.PrimaryModel,
            fallback,
            settings.SystemPromptOverride,
            settings.HandoffConfidenceThreshold,
            settings.SentimentEscalationEnabled,
            settings.SentimentSensitivity,
            phrases,
            settings.MaxUnansweredMessages,
            settings.PiiMaskingLevel,
            settings.DataRetentionDays,
            settings.UpdatedAt);
    }

    private static WorkspaceChatSettingsDto Defaults() => new(
        null,
        AiProviderType.OpenAI,
        "gpt-4o-mini",
        Array.Empty<AiProviderType>(),
        null,
        0.65,
        true,
        SentimentSensitivity.Medium,
        Array.Empty<string>(),
        3,
        PiiMaskingLevel.Standard,
        null,
        null);
}
