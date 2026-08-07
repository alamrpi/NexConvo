using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Ai.Models;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using NSubstitute;

namespace NexConvo.Chat.IntegrationTests.Realtime;

/// <summary>
/// End-to-end: an inbound message event drives the RAG pipeline, and a client joined to the
/// conversation group receives the reply as a sequence of token envelopes followed by exactly one
/// complete envelope carrying the persisted message id, citations, and confidence.
/// </summary>
[Collection("ChatApi")]
public class ChatHubStreamingTests(ChatApiFactory factory)
{
    [Fact]
    public async Task InboundMessage_StreamsTokenEnvelopesThenSingleCompleteWithCitationsAndConfidence()
    {
        var tenantId = Guid.NewGuid();
        var senderId = $"sender-{Guid.NewGuid():N}";
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.3);
        var conversationId = await factory.SeedConversationAsync(tenantId, senderId);

        // Multi-chunk stream so per-token fan-out (vs one buffered send) is observable.
        var aiProvider = Substitute.For<IAiProviderService>();
        aiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("Refunds ", "take 5 business days.", " [1]"));
        factory.AiProviderFactoryMock.GetProvider(Arg.Any<AiProviderType>()).Returns(aiProvider);

        var envelopes = new ConcurrentQueue<JsonElement>();
        await using var connection = factory.CreateHubConnection(
            ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read"));
        connection.On<JsonElement>("chatEvent", envelopes.Enqueue);
        await connection.StartAsync();
        await connection.InvokeAsync("JoinConversation", conversationId);

        await factory.PublishAsync(new MessageReceivedIntegrationEvent
        {
            ConversationId = conversationId,
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = senderId,
            MessageRef = $"ref-{Guid.NewGuid():N}",
            Body = "How long do refunds take?",
            ProviderMessageId = $"provider-{Guid.NewGuid():N}",
        });

        var complete = await PollUntilAsync(() => Task.FromResult(
            envelopes.FirstOrDefault(e => e.GetProperty("type").GetString() == "complete") is { ValueKind: not JsonValueKind.Undefined } c
                ? (JsonElement?)c
                : null));
        complete.Should().NotBeNull("the client should receive a complete envelope after the tokens");

        var received = envelopes.ToArray();
        var tokenEnvelopes = received.Where(e => e.GetProperty("type").GetString() == "token").ToArray();
        tokenEnvelopes.Should().HaveCountGreaterThanOrEqualTo(2, "each chunk is fanned out unbuffered");
        Array.FindIndex(received, e => e.GetProperty("type").GetString() == "token").Should().BeLessThan(
            Array.FindIndex(received, e => e.GetProperty("type").GetString() == "complete"),
            "tokens stream before the completion event");

        var streamedText = string.Concat(tokenEnvelopes.Select(e => e.GetProperty("payload").GetProperty("text").GetString()));
        streamedText.Should().Be("Refunds take 5 business days. [1]");
        tokenEnvelopes.Should().OnlyContain(e =>
            e.GetProperty("payload").GetProperty("conversationId").GetGuid() == conversationId);

        received.Count(e => e.GetProperty("type").GetString() == "complete").Should().Be(1);
        var payload = complete!.Value.GetProperty("payload");
        payload.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        payload.GetProperty("text").GetString().Should().Be("Refunds take 5 business days. [1]");
        payload.GetProperty("confidenceScore").GetDouble().Should().Be(0.9);
        payload.GetProperty("confidenceBand").GetString().Should().Be("High");

        var citations = payload.GetProperty("citations").EnumerateArray().ToArray();
        citations.Should().HaveCount(1);
        citations[0].GetProperty("index").GetInt32().Should().Be(1);
        citations[0].GetProperty("chunkId").GetString().Should().Be("chunk-1");
        citations[0].GetProperty("documentId").GetString().Should().Be("doc-1");
        citations[0].GetProperty("score").GetDouble().Should().Be(0.9);

        // Persistence stays authoritative: the envelope's messageId is the persisted row's id.
        await using var db = factory.CreateDbContext(tenantId);
        var persisted = await db.Messages.FirstOrDefaultAsync(m =>
            m.ConversationId == conversationId && m.Sender.Role == MessageSenderRole.Ai);
        persisted.Should().NotBeNull();
        payload.GetProperty("messageId").GetGuid().Should().Be(persisted!.Id);
    }

    private static async IAsyncEnumerable<AiStreamChunk> StreamOf(params string[] chunks)
    {
        foreach (var c in chunks)
        {
            yield return new AiStreamChunk(c, Reason: null, PromptTokens: null, CompletionTokens: null);
            await Task.Yield();
        }
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
