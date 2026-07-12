using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.IntegrationTests;

[Collection("ChatApi")]
public class MessageReceivedConsumerIdempotencyTests(ChatApiFactory factory)
{
    [Fact]
    public async Task RedeliveredMessage_SameTransportMessageId_DoesNotCreateDuplicateMessage()
    {
        var tenantId = Guid.NewGuid();
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.3);

        var messageId = Guid.NewGuid();
        var evt = new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = "sender-2",
            MessageRef = "ref-2",
            Body = "How long do refunds take?",
            ProviderMessageId = "provider-2",
        };

        await factory.PublishAsync(evt, messageId);
        await factory.PublishAsync(evt, messageId); // redelivery, same transport MessageId

        await using var db = factory.CreateDbContext(tenantId);

        // Give the consumer time to process both deliveries (the second should be deduped by the
        // MassTransit EF inbox before ever reaching MessageReceivedConsumer.Consume).
        int inboundCount = 0;
        for (var i = 0; i < 20; i++)
        {
            inboundCount = await db.Messages.CountAsync(m => m.ProviderMessageId == "provider-2");
            if (inboundCount > 0) break;
            await Task.Delay(500);
        }
        await Task.Delay(1500); // extra settle time in case the duplicate is still in flight

        inboundCount = await db.Messages.CountAsync(m => m.ProviderMessageId == "provider-2");
        inboundCount.Should().Be(1);
    }
}
