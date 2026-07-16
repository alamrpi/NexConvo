using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.ChatSettings.Commands;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;
using NSubstitute;
using System.Text.Json;

namespace NexConvo.Chat.Application.UnitTests.Features.ChatSettings.Commands;

public class SaveChatSettingsCommandHandlerTests
{
    private static SaveChatSettingsCommand NewCommand(Guid actorUserId, List<string> triggerPhrases) =>
        new(
            AiProviderType.OpenAI,
            "gpt-4o-mini",
            FallbackProviders: [],
            SystemPromptOverride: null,
            HandoffConfidenceThreshold: 0.5,
            SentimentEscalationEnabled: false,
            SentimentSensitivity.Medium,
            triggerPhrases,
            MaxUnansweredMessages: 3,
            PiiMaskingLevel.Off,
            DataRetentionDays: null,
            WidgetIconUrl: null,
            WidgetPrimaryColor: "#0F172A",
            WidgetSecondaryColor: "#3B82F6",
            WidgetWelcomeMessage: "Hi there! How can I help you today?",
            NoAnswerMessage: "Sorry, I don't have information about that. Please contact our support team for help.",
            actorUserId);

    private static (SaveChatSettingsCommandHandler Handler, IChatDbContext Db) BuildSut(
        Guid tenantId, WorkspaceChatSettings? existing = null)
    {
        var store = existing is null ? [] : new List<WorkspaceChatSettings> { existing };
        var settingsSet = store.AsQueryable().BuildMockDbSet();
        var auditLogsSet = new List<ChatAuditLog>().AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.WorkspaceChatSettings.Returns(settingsSet);
        db.ChatAuditLogs.Returns(auditLogsSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);

        var handler = new SaveChatSettingsCommandHandler(db, tenant, Substitute.For<ILogger<SaveChatSettingsCommandHandler>>());
        return (handler, db);
    }

    [Fact]
    public async Task Handle_FirstCreate_WithNoTriggerPhrases_SeedsBuiltInDefaults()
    {
        var tenantId = Guid.NewGuid();
        var (handler, db) = BuildSut(tenantId);

        await handler.Handle(NewCommand(Guid.NewGuid(), triggerPhrases: []), CancellationToken.None);

        var expectedJson = JsonSerializer.Serialize(DefaultTriggerPhrases.Values);
        db.WorkspaceChatSettings.Received(1).Add(Arg.Is<WorkspaceChatSettings>(s => s.TriggerPhrases == expectedJson));
    }

    [Fact]
    public async Task Handle_FirstCreate_WithExplicitTriggerPhrases_PersistsExactlyWhatWasSent()
    {
        var tenantId = Guid.NewGuid();
        var (handler, db) = BuildSut(tenantId);

        await handler.Handle(NewCommand(Guid.NewGuid(), triggerPhrases: ["custom phrase"]), CancellationToken.None);

        var expectedJson = JsonSerializer.Serialize(new[] { "custom phrase" });
        db.WorkspaceChatSettings.Received(1).Add(Arg.Is<WorkspaceChatSettings>(s => s.TriggerPhrases == expectedJson));
    }

    [Fact]
    public async Task Handle_WhenConcurrentUpdateConflicts_ReturnsConflict_NotUnhandled()
    {
        var tenantId = Guid.NewGuid();
        var existing = new WorkspaceChatSettings(
            tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false,
            SentimentSensitivity.Medium, "[]", 3, PiiMaskingLevel.Off, null);
        var (handler, db) = BuildSut(tenantId, existing);
        db.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new DbUpdateConcurrencyException("stale row"));

        var result = await handler.Handle(NewCommand(Guid.NewGuid(), triggerPhrases: []), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(NexConvo.BuildingBlocks.Results.ResultStatus.Conflict);
    }

    [Fact]
    public async Task Handle_UpdateExistingSettings_WithEmptyTriggerPhrases_PersistsEmptyAndNeverReSeedsDefaults()
    {
        var tenantId = Guid.NewGuid();
        var existing = new WorkspaceChatSettings(
            tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false,
            SentimentSensitivity.Medium, JsonSerializer.Serialize(DefaultTriggerPhrases.Values),
            3, PiiMaskingLevel.Off, null);
        var (handler, _) = BuildSut(tenantId, existing);

        await handler.Handle(NewCommand(Guid.NewGuid(), triggerPhrases: []), CancellationToken.None);

        existing.TriggerPhrases.Should().Be("[]");
    }
}
