using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using NSubstitute;

namespace NexConvo.Chat.IntegrationTests;

[Collection("ChatApi")]
public class RagReplyEndToEndTests(ChatApiFactory factory)
{
    [Fact]
    public async Task GroundedQuestion_PersistsAiReplyWithAuditRowAndPublishesNoHandoff()
    {
        var tenantId = Guid.NewGuid();
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.3);

        await factory.PublishAsync(new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = "sender-1",
            MessageRef = "ref-1",
            Body = "How long do refunds take?",
            ProviderMessageId = "provider-1",
        });

        await using var db = factory.CreateDbContext(tenantId);

        var reply = await PollUntilAsync(
            () => db.Messages.FirstOrDefaultAsync(m => m.Sender.Role == MessageSenderRole.Ai));
        reply.Should().NotBeNull();
        reply!.Body.Should().Contain("[1]");

        var auditRows = await db.ChatAuditLogs.ToListAsync();
        auditRows.Should().NotBeEmpty();

        var conversation = await db.Conversations.FirstAsync();
        conversation.State.Should().Be(ConversationState.AiHandling);

        var escalations = await db.Escalations.ToListAsync();
        escalations.Should().BeEmpty();

        // Finding 2 regression guard: the consumer-path gRPC call must carry the message's own
        // tenant, never one resolved from an ambient/HTTP-scoped context (there is no HttpContext
        // on this path).
        await factory.KnowledgeMock.Received(1).SearchAsync(
            tenantId, Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnansweredQuestion_HandsOffToHuman_NoAiReplySent()
    {
        var tenantId = Guid.NewGuid();
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.99);

        await factory.PublishAsync(new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = "sender-3",
            MessageRef = "ref-3",
            Body = "What is your CEO's home address?",
            ProviderMessageId = "provider-3",
        });

        await using var db = factory.CreateDbContext(tenantId);

        var conversation = await PollUntilAsync(async () =>
        {
            var c = await db.Conversations.FirstOrDefaultAsync();
            return c is { State: ConversationState.PendingHuman } ? c : null;
        });
        conversation.Should().NotBeNull();

        var aiReply = await db.Messages.FirstOrDefaultAsync(m => m.Sender.Role == MessageSenderRole.Ai);
        aiReply.Should().BeNull();

        var escalations = await db.Escalations.ToListAsync();
        escalations.Should().ContainSingle(e => e.Reason == EscalationReason.LowConfidence);
    }

    private static async Task<T?> PollUntilAsync<T>(Func<Task<T?>> probe, int attempts = 40, int delayMs = 1000)
    {
        for (var i = 0; i < attempts; i++)
        {
            var result = await probe();
            if (result is not null) return result;
            await Task.Delay(delayMs);
        }
        return default;
    }
}
