using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.IntegrationTests.Realtime;

/// <summary>
/// A PendingHuman transition must light up agent dashboards in real time: the handoff envelope is
/// broadcast to tenant:{tenantId}:agents (and to the conversation group, so live viewers learn
/// the streamed draft was suppressed) — and only after the escalation is persisted.
/// </summary>
[Collection("ChatApi")]
public class ChatHubHandoffTests(ChatApiFactory factory)
{
    [Fact]
    public async Task LowConfidenceReply_BroadcastsHandoffEnvelopeToAgentDashboardAndConversationGroup()
    {
        var tenantId = Guid.NewGuid();
        var senderId = $"sender-{Guid.NewGuid():N}";
        // Threshold above the mocked 0.9 retrieval score forces the LowConfidence handoff path.
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.99);
        var conversationId = await factory.SeedConversationAsync(tenantId, senderId);

        // Agent dashboard connection — joined to the agents group only.
        var agentEnvelopes = new ConcurrentQueue<JsonElement>();
        await using var agentConnection = factory.CreateHubConnection(
            ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read"));
        agentConnection.On<JsonElement>("chatEvent", agentEnvelopes.Enqueue);
        await agentConnection.StartAsync();
        await agentConnection.InvokeAsync("JoinAgentDashboard");

        // Conversation viewer — joined to the conversation group only.
        var viewerEnvelopes = new ConcurrentQueue<JsonElement>();
        await using var viewerConnection = factory.CreateHubConnection(
            ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read"));
        viewerConnection.On<JsonElement>("chatEvent", viewerEnvelopes.Enqueue);
        await viewerConnection.StartAsync();
        await viewerConnection.InvokeAsync("JoinConversation", conversationId);

        await factory.PublishAsync(new MessageReceivedIntegrationEvent
        {
            ConversationId = conversationId,
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = senderId,
            MessageRef = $"ref-{Guid.NewGuid():N}",
            Body = "What is your CEO's home address?",
            ProviderMessageId = $"provider-{Guid.NewGuid():N}",
        });

        var agentHandoff = await PollUntilAsync(() => Task.FromResult(FindHandoff(agentEnvelopes)));
        agentHandoff.Should().NotBeNull("agent dashboards must be notified of the PendingHuman transition in real time");

        var payload = agentHandoff!.Value.GetProperty("payload");
        payload.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        payload.GetProperty("reason").GetString().Should().Be("LowConfidence");
        var escalationId = payload.GetProperty("escalationId").GetGuid();
        escalationId.Should().NotBeEmpty();

        // Broadcast happens only after persist: the escalation row and state are already committed.
        await using var db = factory.CreateDbContext(tenantId);
        var conversation = await db.Conversations.FirstAsync(c => c.Id == conversationId);
        conversation.State.Should().Be(ConversationState.PendingHuman);
        var escalation = await db.Escalations.FirstOrDefaultAsync(e => e.Id == escalationId);
        escalation.Should().NotBeNull("the broadcast escalationId must reference a persisted row");
        escalation!.Reason.Should().Be(EscalationReason.LowConfidence);

        // The conversation group receives the same envelope (its signal to discard the draft).
        var viewerHandoff = await PollUntilAsync(() => Task.FromResult(FindHandoff(viewerEnvelopes)));
        viewerHandoff.Should().NotBeNull("conversation viewers must learn the reply was suppressed");
        viewerHandoff!.Value.GetProperty("payload").GetProperty("escalationId").GetGuid().Should().Be(escalationId);
    }

    private static JsonElement? FindHandoff(ConcurrentQueue<JsonElement> envelopes) =>
        envelopes.FirstOrDefault(e => e.GetProperty("type").GetString() == "handoff") is { ValueKind: not JsonValueKind.Undefined } h
            ? h
            : null;

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
