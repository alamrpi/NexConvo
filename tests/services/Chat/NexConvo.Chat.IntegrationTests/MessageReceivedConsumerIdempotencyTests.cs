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

        // Wait for the count to be both non-zero AND stable across consecutive polls, rather than
        // a fixed sleep — a blind "settle time" races against however long the (deduped) second
        // delivery actually takes to be rejected by the inbox, which is a flakiness risk on a
        // slow/loaded CI runner. Stability across several consecutive polls is a much stronger
        // signal that no duplicate is still in flight.
        const int requiredStableReads = 3;
        var stableReads = 0;
        var lastCount = -1;
        var inboundCount = 0;
        for (var i = 0; i < 40 && stableReads < requiredStableReads; i++)
        {
            inboundCount = await db.Messages.CountAsync(m => m.ProviderMessageId == "provider-2");
            stableReads = inboundCount > 0 && inboundCount == lastCount ? stableReads + 1 : 0;
            lastCount = inboundCount;
            await Task.Delay(250);
        }

        inboundCount.Should().Be(1);
    }
}
