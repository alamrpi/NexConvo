using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.IntegrationTests.Realtime;

/// <summary>
/// add-chat-inbox, Task 4: WidgetHub.SendMessageAsync must publish a MessageReceivedIntegrationEvent
/// for every inbound visitor message so the durable MessageReceivedConsumer persists a Conversation/
/// Message pair independently of the widget's own ephemeral token stream — otherwise widget
/// conversations never appear in the agent inbox.
/// </summary>
[Collection("ChatApi")]
public class WidgetHubPersistenceTests(ChatApiFactory factory)
{
    [Fact]
    public async Task SendMessageAsync_PersistsConversationAndInboundMessage_ForWebChannel()
    {
        var tenantId = Guid.NewGuid();
        var widgetToken = await factory.SeedWidgetTenantAsync(tenantId);

        await using var connection = factory.CreateWidgetHubConnection(widgetToken);
        var completed = new TaskCompletionSource();
        connection.On(WidgetHubClientMethods.ReceiveCompleted, () => completed.TrySetResult());
        connection.On<string>(WidgetHubClientMethods.ReceiveError, _ => completed.TrySetResult());
        await connection.StartAsync();

        await connection.InvokeAsync("SendMessageAsync", "How long do refunds take?");

        // The visitor's own ephemeral stream finishing is not proof the persistence publish landed
        // (it is fire-and-forget and unrelated to the RAG reply path) — poll the database instead.
        var persisted = await PollUntilAsync(async () =>
        {
            await using var db = factory.CreateDbContext(tenantId);
            return await db.Conversations
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Channel.Channel == LeadSourceChannel.Web);
        });

        persisted.Should().NotBeNull("the inbound widget message should create a Conversation via MessageReceivedConsumer");
        persisted!.Channel.Channel.Should().Be(LeadSourceChannel.Web);

        await using var messagesDb = factory.CreateDbContext(tenantId);
        var inbound = await messagesDb.Messages
            .SingleOrDefaultAsync(m => m.ConversationId == persisted.Id && m.Direction == MessageDirection.Inbound);
        inbound.Should().NotBeNull();
        inbound!.Body.Should().Be("How long do refunds take?");

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task SendMessageAsync_TwoMessagesOnSameConnection_AppendToTheSameConversation()
    {
        var tenantId = Guid.NewGuid();
        var widgetToken = await factory.SeedWidgetTenantAsync(tenantId);

        await using var connection = factory.CreateWidgetHubConnection(widgetToken);
        await connection.StartAsync();

        await connection.InvokeAsync("SendMessageAsync", "First message");
        await connection.InvokeAsync("SendMessageAsync", "Second message");

        // Stable-per-connection identity (Context.ConnectionId): repeated messages on the SAME
        // connection must find-or-create exactly one open Conversation, not one per message.
        var conversation = await PollUntilAsync(async () =>
        {
            await using var db = factory.CreateDbContext(tenantId);
            var conversations = await db.Conversations
                .Where(c => c.TenantId == tenantId && c.Channel.Channel == LeadSourceChannel.Web)
                .ToListAsync();

            if (conversations.Count != 1) return null;

            var inboundCount = await db.Messages.CountAsync(
                m => m.ConversationId == conversations[0].Id && m.Direction == MessageDirection.Inbound);

            return inboundCount == 2 ? conversations[0] : null;
        });

        conversation.Should().NotBeNull(
            "both messages on one widget connection should append to the same Conversation, not create two");
    }

    private static async Task<T?> PollUntilAsync<T>(Func<Task<T?>> probe, int attempts = 40, int delayMs = 500)
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

/// <summary>Mirrors WidgetHub's private client-method-name constants for the test's .On registrations.</summary>
internal static class WidgetHubClientMethods
{
    public const string ReceiveCompleted = "receiveCompleted";
    public const string ReceiveError = "receiveError";
}
