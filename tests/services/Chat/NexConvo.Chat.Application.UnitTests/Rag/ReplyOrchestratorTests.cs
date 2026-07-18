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
        IPublishEndpoint Publisher,
        IReplyStreamSink StreamSink,
        IGroundingGate GroundingGate,
        IGreetingDetector GreetingDetector);

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
        var auditLogs = new List<ChatAuditLog>().AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversations);
        db.WorkspaceChatSettings.Returns(settingsSet);
        db.Messages.Returns(messages);
        db.Escalations.Returns(escalations);
        db.ChatAuditLogs.Returns(auditLogs);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var knowledge = Substitute.For<IKnowledgeRetrievalClient>();
        knowledge.SearchAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
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

        var dbFactory = Substitute.For<IChatDbContextFactory>();
        dbFactory.CreateForTenant(conversation.TenantId).Returns(db);

        var streamSink = Substitute.For<IReplyStreamSink>();

        var groundingGate = Substitute.For<IGroundingGate>();
        groundingGate.ShouldAnswer(Arg.Any<IReadOnlyList<KnowledgeChunkMatch>>(), Arg.Any<ChannelProfile>()).Returns(true);

        var greetingDetector = Substitute.For<IGreetingDetector>();
        greetingDetector.IsGreeting(Arg.Any<string>()).Returns(false);

        var sut = new ReplyOrchestrator(
            dbFactory, knowledge, aiFactory, cache, aes,
            new GroundedPromptAssembler(), new TokenBudgeter(), publisher, streamSink,
            groundingGate, greetingDetector, Substitute.For<ILogger<ReplyOrchestrator>>());

        return new SutContext(sut, db, knowledge, aiFactory, cache, aiProvider, publisher, streamSink, groundingGate, greetingDetector);
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

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<AnsweredOutcome>();
        ((AnsweredOutcome)outcome).Text.Should().Contain("[1]");
    }

    [Fact]
    public async Task RunAsync_GroundedQuestion_WritesAuditLogRow()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        ctx.Db.ChatAuditLogs.Received(1).Add(Arg.Is<ChatAuditLog>(a =>
            a.TenantId == tenantId && a.Action == "chat.rag.answered"));
    }

    [Fact]
    public async Task RunAsync_TriggerPhraseMatched_WritesAuditLogRow()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "I want to speak to a manager");
        var settings = NewSettings(tenantId, triggerPhrases: "[\"speak to a manager\"]");

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        ctx.Db.ChatAuditLogs.Received(1).Add(Arg.Is<ChatAuditLog>(a =>
            a.TenantId == tenantId && a.Action == "chat.rag.handoff-requested"));
    }

    [Fact]
    public async Task RunAsync_Greeting_AnswersDirectlyWithoutCallingProviderOrKnowledge()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "hi");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.GreetingDetector.IsGreeting("hi").Returns(true);

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<AnsweredOutcome>();
        await ctx.Knowledge.DidNotReceive().SearchAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
        ctx.AiFactory.DidNotReceive().GetProvider(Arg.Any<AiProviderType>());
        ctx.Db.ChatAuditLogs.Received(1).Add(Arg.Is<ChatAuditLog>(a =>
            a.TenantId == tenantId && a.Action == "chat.rag.answered"));
    }

    [Fact]
    public async Task RunAsync_GroundedQuestion_PassesRunAsyncTenantIdToKnowledgeSearch()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        // Finding 2 regression guard: the tenant sent to Knowledge must be the caller-supplied
        // RunAsync tenantId, never resolved from an ambient/HTTP-scoped context.
        await ctx.Knowledge.Received(1).SearchAsync(
            tenantId, Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
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

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.LowConfidence);
    }

    [Theory]
    [InlineData("I'm sorry, but [[NO_ANSWER]]")]
    [InlineData("[[no_answer]]")]
    [InlineData("\"[[NO_ANSWER]]\"")]
    public async Task RunAsync_ModelAbstainsWithWrappedOrCasedMarker_ReturnsHandoffOutcomeLowConfidence(string reply)
    {
        // D4-3 regression guard: abstention detection must not be a strict, case-sensitive prefix
        // match — models sometimes preface, quote, or vary the case of the marker.
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "What is your CEO's home address?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.AiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf(reply));

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

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

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.TriggerPhrase);
        await ctx.Knowledge.DidNotReceive().SearchAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
        ctx.AiFactory.DidNotReceive().GetProvider(Arg.Any<AiProviderType>());
    }

    [Fact]
    public async Task RunAsync_TriggerPhraseMatched_PublishesHandoffIntegrationEvent()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "I want to speak to a manager");
        var settings = NewSettings(tenantId, triggerPhrases: "[\"speak to a manager\"]");

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

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

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.NoAiConfig);
    }

    [Fact]
    public async Task RunAsync_GroundingGateRejects_HandsOffAndNeverCallsProvider()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "What is your CEO's home address?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.Knowledge.SearchAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([new KnowledgeChunkMatch("chunk-1", "doc-1", "Unrelated low-score content.", 0.1)]);
        ctx.GroundingGate.ShouldAnswer(Arg.Any<IReadOnlyList<KnowledgeChunkMatch>>(), Arg.Any<ChannelProfile>()).Returns(false);

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(EscalationReason.LowConfidence);
        ctx.AiFactory.DidNotReceive().GetProvider(Arg.Any<AiProviderType>());
        ctx.AiProvider.DidNotReceive().GenerateStreamAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        ctx.Db.Escalations.Received(1).Add(Arg.Is<Escalation>(e => e.Reason == EscalationReason.LowConfidence));
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

        var act = async () => await ctx.Sut.RunAsync(tenantId, conversation.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ctx.Db.Messages.DidNotReceive().Add(Arg.Any<Message>());
    }

    [Fact]
    public async Task RunAsync_StreamedChunks_AreFannedToSinkPerChunkInOrder()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.AiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("Refunds ", "take 5 days.", " [1]"));

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        Received.InOrder(() =>
        {
            ctx.StreamSink.OnTokenAsync(tenantId, conversation.Id, "Refunds ", Arg.Any<CancellationToken>());
            ctx.StreamSink.OnTokenAsync(tenantId, conversation.Id, "take 5 days.", Arg.Any<CancellationToken>());
            ctx.StreamSink.OnTokenAsync(tenantId, conversation.Id, " [1]", Arg.Any<CancellationToken>());
        });
        await ctx.StreamSink.Received(3).OnTokenAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AnsweredReply_CallsOnCompletedAfterSaveWithCitationsAndConfidence()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        var answered = outcome.Should().BeOfType<AnsweredOutcome>().Subject;
        await ctx.StreamSink.Received(1).OnCompletedAsync(
            tenantId,
            conversation.Id,
            Arg.Is<ReplyCompletedNotification>(n =>
                n.MessageId == answered.MessageId &&
                n.Text == answered.Text &&
                n.Confidence == answered.Confidence &&
                n.Citations.Count == 1 &&
                n.Citations[0].Index == 1 &&
                n.Citations[0].ChunkId == "chunk-1" &&
                n.Citations[0].DocumentId == "doc-1" &&
                n.Citations[0].Score == 0.9),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            ctx.Db.SaveChangesAsync(Arg.Any<CancellationToken>());
            ctx.StreamSink.OnCompletedAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<ReplyCompletedNotification>(), Arg.Any<CancellationToken>());
        });
        await ctx.StreamSink.DidNotReceive().OnHandoffAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<HandoffNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_RetrievalOverBudget_CitationsReflectOnlyTheFittedContextSentToTheLlm()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));

        // ChannelProfile.Chat's budget is MaxAnswerTokens(600) * 4 = 2400 tokens. Five ~700-token
        // chunks (~3500 total) forces TokenBudgeter.Fit to drop the lowest-scored ones. D4-2
        // regression guard: the client-facing citations must reflect only what survived Fit
        // (chunk-1/chunk-2, the two highest-scored), never the full pre-budget retrieval list.
        var oversizedContent = string.Join(" ", Enumerable.Repeat("refund policy details", 350));
        ctx.Knowledge.SearchAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new KnowledgeChunkMatch("chunk-1", "doc-1", oversizedContent, 0.95),
                new KnowledgeChunkMatch("chunk-2", "doc-2", oversizedContent, 0.90),
                new KnowledgeChunkMatch("chunk-3", "doc-3", oversizedContent, 0.85),
                new KnowledgeChunkMatch("chunk-4", "doc-4", oversizedContent, 0.80),
                new KnowledgeChunkMatch("chunk-5", "doc-5", oversizedContent, 0.75),
            ]);

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        await ctx.StreamSink.Received(1).OnCompletedAsync(
            tenantId,
            conversation.Id,
            Arg.Is<ReplyCompletedNotification>(n =>
                n.Citations.Count < 5 &&
                n.Citations.All(c => c.ChunkId != "chunk-4" && c.ChunkId != "chunk-5")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LowConfidence_CallsOnHandoffAfterPersistNotOnCompleted()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "What is your CEO's home address?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.AiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("[[NO_ANSWER]]"));

        var outcome = await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        var handoff = outcome.Should().BeOfType<HandoffOutcome>().Subject;
        await ctx.StreamSink.Received(1).OnHandoffAsync(
            tenantId,
            conversation.Id,
            Arg.Is<HandoffNotification>(n =>
                n.EscalationId == handoff.EscalationId && n.Reason == "LowConfidence"),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            ctx.Db.SaveChangesAsync(Arg.Any<CancellationToken>());
            ctx.Publisher.Publish(Arg.Any<ConversationHandoffRequestedIntegrationEvent>(), Arg.Any<CancellationToken>());
            ctx.StreamSink.OnHandoffAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<HandoffNotification>(), Arg.Any<CancellationToken>());
        });
        await ctx.StreamSink.DidNotReceive().OnCompletedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<ReplyCompletedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_EmptyContentChunks_AreNotFannedToSink()
    {
        var tenantId = Guid.NewGuid();
        var (conversation, inbound) = NewConversationWithInbound(tenantId, "How long do refunds take?");
        var settings = NewSettings(tenantId);

        var ctx = BuildSut(conversation, inbound, settings, CachedAiConfigJson(tenantId));
        ctx.AiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("", "Refunds take 5 business days. [1]"));

        await ctx.Sut.RunAsync(tenantId, conversation.Id, CancellationToken.None);

        await ctx.StreamSink.Received(1).OnTokenAsync(
            tenantId, conversation.Id, "Refunds take 5 business days. [1]", Arg.Any<CancellationToken>());
        await ctx.StreamSink.DidNotReceive().OnTokenAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), "", Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<AiStreamChunk> ThrowingStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new AiStreamChunk("partial", Reason: null, PromptTokens: null, CompletionTokens: null);
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
    }
}
