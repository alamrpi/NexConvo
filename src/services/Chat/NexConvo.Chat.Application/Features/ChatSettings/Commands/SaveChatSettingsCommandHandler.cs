using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using System.Text.Json;

namespace NexConvo.Chat.Application.Features.ChatSettings.Commands;

public sealed class SaveChatSettingsCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    ILogger<SaveChatSettingsCommandHandler> logger)
    : IRequestHandler<SaveChatSettingsCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveChatSettingsCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var fallbackJson = JsonSerializer.Serialize(cmd.FallbackProviders.Select(p => p.ToString()).ToList());
        var phrasesJson  = JsonSerializer.Serialize(cmd.TriggerPhrases);

        var settings = await db.WorkspaceChatSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (settings is null)
        {
            // D4-8: only at first-create, and only when the caller sent no phrases of their own,
            // seed the built-in defaults so a tenant who never touches this field still gets
            // trigger-phrase handoff out of the box. Any later save — even an intentional empty
            // list — is persisted as sent via the Update branch below, never re-seeded.
            var firstCreatePhrasesJson = cmd.TriggerPhrases.Count > 0
                ? phrasesJson
                : JsonSerializer.Serialize(DefaultTriggerPhrases.Values);

            settings = new WorkspaceChatSettings(
                tenantId,
                cmd.PrimaryProvider,
                cmd.PrimaryModel,
                fallbackJson,
                cmd.SystemPromptOverride,
                cmd.HandoffConfidenceThreshold,
                cmd.SentimentEscalationEnabled,
                cmd.SentimentSensitivity,
                firstCreatePhrasesJson,
                cmd.MaxUnansweredMessages,
                cmd.PiiMaskingLevel,
                cmd.DataRetentionDays,
                cmd.WidgetIconUrl,
                cmd.WidgetPrimaryColor,
                cmd.WidgetSecondaryColor,
                cmd.WidgetWelcomeMessage);

            settings.CreatedByUserId = cmd.ActorUserId;
            db.WorkspaceChatSettings.Add(settings);

            db.ChatAuditLogs.Add(new ChatAuditLog(
                "chat.settings.create",
                tenantId,
                cmd.ActorUserId,
                $"provider={cmd.PrimaryProvider},model={cmd.PrimaryModel}",
                DateTimeOffset.UtcNow));
        }
        else
        {
            settings.Update(
                cmd.PrimaryProvider,
                cmd.PrimaryModel,
                fallbackJson,
                cmd.SystemPromptOverride,
                cmd.HandoffConfidenceThreshold,
                cmd.SentimentEscalationEnabled,
                cmd.SentimentSensitivity,
                phrasesJson,
                cmd.MaxUnansweredMessages,
                cmd.PiiMaskingLevel,
                cmd.DataRetentionDays,
                cmd.WidgetIconUrl,
                cmd.WidgetPrimaryColor,
                cmd.WidgetSecondaryColor,
                cmd.WidgetWelcomeMessage);

            db.ChatAuditLogs.Add(new ChatAuditLog(
                "chat.settings.update",
                tenantId,
                cmd.ActorUserId,
                $"provider={cmd.PrimaryProvider},model={cmd.PrimaryModel}",
                DateTimeOffset.UtcNow));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two admins edited the settings concurrently; the xmin token caught the stale write
            // (Standard 16). Surface a clean 409 for the client to reload/retry — never a 500.
            logger.LogWarning("Chat settings save conflicted for tenant {TenantId} (concurrent update)", tenantId);
            return Result<Guid>.Conflict("Settings were changed by someone else. Reload and try again.");
        }

        logger.LogInformation("Chat settings {SettingsId} saved for tenant {TenantId}", settings.Id, tenantId);

        return Result.Success(settings.Id);
    }
}
