using System.Text;
using System.Text.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Ai.Models;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Rag;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using NSubstitute;

namespace NexConvo.Chat.Application.UnitTests.Rag;

public class ReplyOrchestratorTests
{
    private sealed record SutContext(
        ReplyOrchestrator Sut,
        IChatDbContext Db,
        IKnowledgeRetrievalClient Knowledge,
        IAiProviderFactory AiFactory,
        IDistributedCache Cache,
        IAiProviderService AiProvider,
        IPublishEndpoint Publisher);

    private static async IAsyncEnumerable<AiStreamChunk> StreamOf(params string[] chunks)
    {
        foreach (var c in chunks)
        {
            yield return new AiStreamChunk(c, Reason: null, PromptTokens: null, CompletionTokens: null);
            await Task.Yield();
        }
    }

    private static SutContext BuildSut(Conversation conversation, Message inboundMessage, WorkspaceChatSettings settings, string cachedAiConfigJson)
    {
        var conversations = new List<Conversation> { conversation }.AsQueryable().BuildMockDbSet();
        var settingsSet = new List<WorkspaceChatSettings> { settings }.AsQueryable().BuildMockDbSet();
        var messages = new List<Message> { inboundMessage }.AsQueryable().BuildMockDbSet();
        var escalations = new List<Escalation>().AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversations);
        db.WorkspaceChatSettings.Returns(settingsSet);
        db.Messages.Returns(messages);
        db.Escalations.Returns(escalations);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var knowledge = Substitute.For<IKnowledgeRetrievalClient>();
        knowledge.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([new KnowledgeChunkMatch("chunk-1", "doc-1", "Refunds are processed within 5 business days.", 0.9)]);

        var aiProvider = Substitute.For<IAiProviderService>();
        aiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("Refunds take 5 business days. [1]"));

        var aiFactory = Substitute.For<IAiProviderFactory>();
        aiFactory.GetProvider(Arg.Any<AiProviderType>()).Returns(aiProvider);

        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(Arg.Is<string>(k => k == $"AiConfig:{conversation.TenantId}"), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedAiConfigJson));

        var aes = Substitute.For<IAesEncryptionService>();
        aes.Decrypt(Arg.Any<string>()).Returns("decrypted-api-key");

        var publisher = Substitute.For<IPublishEndpoint>();

        var sut = new ReplyOrchestrator(
            db, knowledge, aiFactory, cache, aes,
            new GroundedPromptAssembler(), new TokenBudgeter(), publisher,
            Substitute.For<ILogger<ReplyOrchestrator>>());

        return new SutContext(sut, db, knowledge, aiFactory, cache, aiProvider, publisher);
    }

    private static string CachedAiConfigJson(Guid tenantId) => JsonSerializer.Serialize(new
    {
        TenantId = tenantId,
        Provider = "OpenAI",
        EncryptedApiKey = "encrypted-blob",
        BaseUrl = (string?)null,
        DefaultModel = "gpt-4o-mini",
        SystemPrompt = (string?)null,
        Parameters = (string?)null,
        IsActive = true,
    });

    private static WorkspaceChatSettings NewSettings(Guid tenantId, double handoffThreshold = 0.5, string triggerPhrases = "[]") =>
        new(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, handoffThreshold, false,
            SentimentSensitivity.Medium, triggerPhrases, 3, PiiMaskingLevel.Off, null);

    private static (Conversation Conversation, Message Inbound) NewConversationWithInbound(Guid tenantId, string body)
    {
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "ext-1"), null);
        var inbound = conversation.AppendInbound("provider-1", body);
        return (conversation, inbound);
    }

    [Fact]
    public async Task RunAsync_GroundedQuestion_ReturnsAnsweredOutcomeWithCitation()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        var outcome = await ctx.Sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<AnsweredOutcome>();
        ((AnsweredOutcome)outcome).Text.Should().Contain("[1]");
    }

    [Fact]
    public async Task RunAsync_ModelAbstains_ReturnsHandoffOutcomeLowConfidence()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "What is your CEO's home address?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.AiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("[[NO_ANSWER]]"));

        var outcome = await ctx.Sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.LowConfidence);
    }

    [Fact]
    public async Task RunAsync_TriggerPhraseMatched_HandsOffWithoutCallingAiProvider()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "I want to speak to a manager");
        var settings = NewSettings(tenantId, triggerPhrases: "[\"speak to a manager\"]");

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        var outcome = await ctx.Sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.TriggerPhrase);
        await ctx.Knowledge.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
        ctx.AiFactory.DidNotReceive().GetProvider(Arg.Any<AiProviderType>());
    }

    [Fact]
    public async Task RunAsync_TriggerPhraseMatched_PublishesHandoffIntegrationEvent()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "I want to speak to a manager");
        var settings = NewSettings(tenantId, triggerPhrases: "[\"speak to a manager\"]");

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        await ctx.Sut.RunAsync(conversation.Id, CancellationToken.None);

        await ctx.Publisher.Received(1).Publish(
            Arg.Is<ConversationHandoffRequestedIntegrationEvent>(e =>
                e.TenantId == tenantId && e.ConversationId == conversation.Id && e.Reason == "TriggerPhrase"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_MissingAiConfig_ReturnsHandoffOutcomeNoAiConfig()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.Cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var outcome = await ctx.Sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.NoAiConfig);
    }

    [Fact]
    public async Task RunAsync_CancellationRequestedMidStream_PropagatesAndDoesNotPersistPartialReply()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.AiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => ThrowingStream());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await ctx.Sut.RunAsync(conversation.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ctx.Db.Messages.DidNotReceive().Add(Arg.Any<Message>());
    }

    private static async IAsyncEnumerable<AiStreamChunk> ThrowingStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new AiStreamChunk("partial", Reason: null, PromptTokens: null, CompletionTokens: null);
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
    }
}
