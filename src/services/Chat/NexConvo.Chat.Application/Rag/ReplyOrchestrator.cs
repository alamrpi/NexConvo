using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Rag;

/// <summary>
/// Retrieval -> grounded prompt -> tenant LLM -> confidence -> answer-or-handoff. One iteration
/// today (loop-ready for a future MCP tool-calling extension); cancelable end-to-end.
/// </summary>
public sealed class ReplyOrchestrator(
    IChatDbContext db,
    IKnowledgeRetrievalClient knowledge,
    IAiProviderFactory aiProviderFactory,
    IDistributedCache cache,
    IAesEncryptionService aes,
    IGroundedPromptAssembler promptAssembler,
    ITokenBudgeter tokenBudgeter,
    ILogger<ReplyOrchestrator> logger) : IReplyOrchestrator
{
    private const string AbstentionMarker = "[[NO_ANSWER]]";

    /// <summary>
    /// Runs one iteration of the grounded-reply pipeline: retrieve, assemble, generate, decide.
    /// A future MCP tool-calling loop (P3) wraps repeated calls to this same iteration body
    /// (feeding tool results back in as additional context) rather than requiring a rewrite —
    /// this method IS that single iteration, not a loop with a single pass baked in.
    /// </summary>
    public async Task<ReplyOutcome> RunAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        {
            var conversation = await db.Conversations.FirstAsync(c => c.Id == conversationId, cancellationToken);
            var settings = await db.WorkspaceChatSettings.FirstAsync(s => s.TenantId == conversation.TenantId, cancellationToken);
            var lastInbound = await db.Messages
                .Where(m => m.ConversationId == conversationId && m.Direction == MessageDirection.Inbound)
                .OrderByDescending(m => m.CreatedAt)
                .FirstAsync(cancellationToken);

            if (conversation.State != ConversationState.AiHandling)
            {
                logger.LogInformation(
                    "Skipping RAG reply for conversation {ConversationId}; state is {State}, not AiHandling",
                    conversationId, conversation.State);
                return new HandoffOutcome(Guid.Empty, EscalationReason.LowConfidence);
            }

            var triggerPhrases = JsonSerializer.Deserialize<string[]>(settings.TriggerPhrases) ?? [];
            if (triggerPhrases.Any(p => lastInbound.Body.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return await HandoffAsync(conversation, EscalationReason.TriggerPhrase, cancellationToken);
            }

            var cachedBytes = await cache.GetAsync($"AiConfig:{conversation.TenantId}", cancellationToken);
            if (cachedBytes is null)
            {
                return await HandoffAsync(conversation, EscalationReason.NoAiConfig, cancellationToken);
            }

            var aiConfig = JsonSerializer.Deserialize<CachedAiConfig>(Encoding.UTF8.GetString(cachedBytes))!;

            var matches = await knowledge.SearchAsync(
                lastInbound.Body, ChannelProfile.Chat.TopK, ChannelProfile.Chat.MinScore, cancellationToken);

            var contributions = matches
                .Select((m, i) => new ContextContribution(i + 1, m.ChunkId, m.DocumentId, m.Content, m.Score))
                .ToList();

            var (fittedContext, fittedHistory) = tokenBudgeter.Fit(contributions, [], ChannelProfile.Chat.MaxAnswerTokens * 4);

            var systemPrompt = promptAssembler.BuildSystemPrompt(ChannelProfile.Chat, settings.SystemPromptOverride);
            var userPrompt = promptAssembler.BuildUserPrompt(fittedContext, fittedHistory, lastInbound.Body, ChannelProfile.Chat);

            var apiKey = aes.Decrypt(aiConfig.EncryptedApiKey);
            var provider = aiProviderFactory.GetProvider(Enum.Parse<AiProviderType>(aiConfig.Provider));

            var replyBuilder = new StringBuilder();
            await foreach (var chunk in provider.GenerateStreamAsync(
                userPrompt, systemPrompt, apiKey, aiConfig.DefaultModel, aiConfig.BaseUrl, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                replyBuilder.Append(chunk.Content);
            }

            var replyText = replyBuilder.ToString();
            var abstained = replyText.TrimStart().StartsWith(AbstentionMarker, StringComparison.Ordinal);
            var topScore = matches.Count > 0 ? matches.Max(m => m.Score) : 0d;
            var confidence = RagConfidence.FromRetrievalAndAbstention(topScore, abstained);

            if (confidence.Score < settings.HandoffConfidenceThreshold || abstained)
            {
                return await HandoffAsync(conversation, EscalationReason.LowConfidence, cancellationToken);
            }

            var message = conversation.AppendAiReply(replyText, confidence, tokens: null);
            db.Messages.Add(message);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "AI reply appended to conversation {ConversationId}, message {MessageId}, confidence {ConfidenceScore}",
                conversationId, message.Id, confidence.Score);

            return new AnsweredOutcome(message.Id, replyText, confidence);
        }
    }

    private async Task<ReplyOutcome> HandoffAsync(Conversation conversation, EscalationReason reason, CancellationToken cancellationToken)
    {
        var escalation = conversation.RequestHandoff(reason);
        db.Escalations.Add(escalation);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Conversation {ConversationId} handed off to human, reason {Reason}",
            conversation.Id, reason);

        return new HandoffOutcome(escalation.Id, reason);
    }

    private sealed record CachedAiConfig(
        Guid TenantId, string Provider, string EncryptedApiKey, string? BaseUrl,
        string DefaultModel, string? SystemPrompt, string? Parameters, bool IsActive);
}
