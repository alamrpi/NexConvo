using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai.Models;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Rag;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Api.Realtime;

public sealed record PlaygroundRequest(
    string UserMessage,
    string ProviderId,
    string ModelId,
    string? SystemPrompt);

public sealed record PlaygroundDebugChunk(
    int Rank,
    string Document,
    string Preview,
    string FullText,
    double Score);

public sealed record PlaygroundDebugTiming(
    long Embed,
    long Retrieval,
    long Llm,
    long Total);

public sealed record PlaygroundPiiToken(
    string Token,
    string Type);

public sealed record PlaygroundDebugData(
    PlaygroundDebugChunk[] Chunks,
    double Confidence,
    string ConfidenceBand,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string PromptPreview,
    string RawResponse,
    PlaygroundPiiToken[] PiiDetected,
    PlaygroundDebugTiming Timing);

[Authorize]
public sealed class PlaygroundHub(
    IKnowledgeRetrievalClient knowledge,
    IAiProviderFactory aiProviderFactory,
    IDistributedCache cache,
    IAesEncryptionService aes,
    IGroundedPromptAssembler promptAssembler,
    ITokenBudgeter tokenBudgeter,
    IGroundingGate groundingGate,
    IAbstentionStreamFilter abstentionFilter,
    IGreetingDetector greetingDetector,
    ILogger<PlaygroundHub> logger) : Hub
{
    // ─── Client method names (mirrors WidgetHub's error-channel convention) ────
    public const string ReceiveToken     = "ReceiveToken";
    public const string ReceiveDebugData = "ReceiveDebugData";
    public const string ReceiveCompleted = "ReceiveCompleted";
    public const string ReceiveError     = "ReceiveError";

    private const string AccessDeniedMessage = "Access denied.";
    private const int MaxUserMessageLength = 4000;
    private const string GreetingReply = "Hi! How can I help you today?";

    public async Task ExecuteScenarioAsync(PlaygroundRequest request)
    {
        var tenantId = GetRequiredTenantId();

        if (string.IsNullOrWhiteSpace(request.UserMessage) || request.UserMessage.Length > MaxUserMessageLength)
        {
            await Clients.Caller.SendAsync(ReceiveError, "Message is empty or too long.", Context.ConnectionAborted);
            return;
        }

        var swTotal = Stopwatch.StartNew();

        logger.LogInformation("Playground execution started for tenant {TenantId}, model {ModelId}", tenantId, request.ModelId);

        // Fetch AI Config for the tenant to get the API Key
        var cachedBytes = await cache.GetAsync($"AiConfig:{tenantId}", Context.ConnectionAborted);
        if (cachedBytes is null)
        {
            await Clients.Caller.SendAsync(ReceiveError, "AI Configuration not found for tenant. Please configure it first.", Context.ConnectionAborted);
            return;
        }

        var aiConfig = JsonSerializer.Deserialize<CachedAiConfig>(Encoding.UTF8.GetString(cachedBytes))!;

        if (!aiConfig.IsActive)
        {
            await Clients.Caller.SendAsync(ReceiveError, "AI assistant is currently disabled.", Context.ConnectionAborted);
            return;
        }

        string apiKey;
        try
        {
            apiKey = aes.Decrypt(aiConfig.EncryptedApiKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt API key for tenant {TenantId}", tenantId);
            await Clients.Caller.SendAsync(ReceiveError, "Configuration error. Please contact support.", Context.ConnectionAborted);
            return;
        }

        // Map UI provider string to enum
        if (!Enum.TryParse<AiProviderType>(request.ProviderId, true, out var providerType))
        {
            // If parse fails, fallback to what's in config or default to OpenRouter
            _ = Enum.TryParse(aiConfig.Provider, true, out providerType);
        }

        var provider = aiProviderFactory.GetProvider(providerType);

        // No DB-backed chat settings on this authenticated hub (unlike WidgetHub) — fall back to the
        // same default fallback copy used everywhere a tenant hasn't customized NoAnswerMessage yet.
        const string noAnswerMessage = "Sorry, I don't have information about that. Please contact our support team for help.";

        try
        {
            // 0. Greeting/small-talk bypass — these have no retrievable KB score and would
            // otherwise always trip the grounding gate below, which reads as broken to a user
            // who just said hello.
            if (greetingDetector.IsGreeting(request.UserMessage))
            {
                await Clients.Caller.SendAsync(ReceiveToken, GreetingReply, Context.ConnectionAborted);
                await Clients.Caller.SendAsync(ReceiveCompleted, Context.ConnectionAborted);
                return;
            }

            // 1. Retrieval
            var swRetrieval = Stopwatch.StartNew();
            // Since we don't have separate Embed timing easily, we split the retrieval time arbitrarily for UI aesthetics or 0 it.
            // IKnowledgeRetrievalClient does both embed and search. Let's just track the whole thing as Retrieval.
            var matches = await knowledge.SearchAsync(
                tenantId, request.UserMessage, ChannelProfile.Chat.TopK, ChannelProfile.Chat.MinScore, Context.ConnectionAborted);
            swRetrieval.Stop();

            // Assume embed takes ~30% of retrieval time for realistic UI visualization
            var embedTiming = (long)(swRetrieval.ElapsedMilliseconds * 0.3);
            var searchTiming = swRetrieval.ElapsedMilliseconds - embedTiming;

            // 1b. Grounding gate — if retrieval is too weak, never call the LLM at all. No human
            // handoff exists in the Playground — it only shows the fallback.
            if (!groundingGate.ShouldAnswer(matches, ChannelProfile.Chat))
            {
                logger.LogInformation("Playground query below grounding gate — returning fallback. Tenant={TenantId}", tenantId);
                await Clients.Caller.SendAsync(ReceiveToken, noAnswerMessage, Context.ConnectionAborted);
                await Clients.Caller.SendAsync(ReceiveCompleted, Context.ConnectionAborted);
                return;
            }

            var contributions = matches
                .Select((m, i) => new ContextContribution(i + 1, m.ChunkId, m.DocumentId, m.Content, m.Score))
                .ToList();

            var (fittedContext, fittedHistory) = tokenBudgeter.Fit(contributions, [], ChannelProfile.Chat.MaxAnswerTokens * 4);

            var systemPrompt = promptAssembler.BuildSystemPrompt(ChannelProfile.Chat, request.SystemPrompt);
            var userPrompt = promptAssembler.BuildUserPrompt(fittedContext, fittedHistory, request.UserMessage, ChannelProfile.Chat);

            // 2. LLM Generation — routed through the abstention filter so a leaked [[NO_ANSWER]]
            // marker never reaches the client; the filter is the source of truth for `abstained`.
            var swLlm = Stopwatch.StartNew();
            var replyBuilder = new StringBuilder();
            var abstained = false;

            var tokenStream = ContentOf(
                provider.GenerateStreamAsync(
                    userPrompt, systemPrompt, apiKey, request.ModelId, aiConfig.BaseUrl, Context.ConnectionAborted),
                Context.ConnectionAborted);

            await foreach (var result in abstentionFilter.FilterAsync(tokenStream, Context.ConnectionAborted))
            {
                Context.ConnectionAborted.ThrowIfCancellationRequested();

                if (result.Abstained)
                {
                    abstained = true;
                    break;
                }

                if (!string.IsNullOrEmpty(result.Token))
                {
                    replyBuilder.Append(result.Token);
                    await Clients.Caller.SendAsync(ReceiveToken, result.Token, Context.ConnectionAborted);
                }
            }

            if (abstained)
            {
                logger.LogInformation("Playground reply abstained — returning fallback. Tenant={TenantId}", tenantId);
                await Clients.Caller.SendAsync(ReceiveToken, noAnswerMessage, Context.ConnectionAborted);
            }

            swLlm.Stop();
            swTotal.Stop();

            var replyText = replyBuilder.ToString();
            var topScore = matches.Count > 0 ? matches.Max(m => m.Score) : 0d;
            var confidence = RagConfidence.FromRetrievalAndAbstention(topScore, abstained);

            // Build Debug Data
            var debugChunks = matches.Select((m, i) => new PlaygroundDebugChunk(
                i + 1,
                $"Doc-{m.DocumentId.ToString()[..8]}", // Simplify document name
                m.Content.Length > 50 ? m.Content[..47] + "..." : m.Content,
                m.Content,
                m.Score
            )).ToArray();

            var timing = new PlaygroundDebugTiming(
                embedTiming,
                searchTiming,
                swLlm.ElapsedMilliseconds,
                swTotal.ElapsedMilliseconds);

            // Approximate — providers don't yet surface real usage counts on the streaming path (codebase-wide gap).
            int promptTokensApprox = (systemPrompt.Length + userPrompt.Length) / 4;
            int completionTokensApprox = replyText.Length / 4;

            var debugData = new PlaygroundDebugData(
                debugChunks,
                confidence.Score,
                confidence.Band.ToString(),
                promptTokensApprox,
                completionTokensApprox,
                promptTokensApprox + completionTokensApprox,
                $"[System]: {systemPrompt}\n\n[User]: {userPrompt}",
                JsonSerializer.Serialize(new { response = replyText }), // Approximate — providers stream text only, no raw payload capture.
                Array.Empty<PlaygroundPiiToken>(), // No PII filtering implemented anywhere in the RAG pipeline yet (codebase-wide gap).
                timing
            );

            await Clients.Caller.SendAsync(ReceiveDebugData, debugData, Context.ConnectionAborted);
            await Clients.Caller.SendAsync(ReceiveCompleted, Context.ConnectionAborted);
            logger.LogInformation("Playground execution completed for tenant {TenantId}", tenantId);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Playground stream cancelled by client. Tenant={TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming playground response. Tenant={TenantId}", tenantId);
            await Clients.Caller.SendAsync(ReceiveError, "An error occurred while generating a response.", Context.ConnectionAborted);
        }
    }

    private Guid GetRequiredTenantId()
    {
        var claim = Context.User?.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        return Guid.TryParse(claim, out var tenantId)
            ? tenantId
            : throw new HubException(AccessDeniedMessage);
    }

    /// <summary>
    /// Adapts the provider's <see cref="AiStreamChunk"/> stream into a plain token stream for
    /// <see cref="IAbstentionStreamFilter"/>. System.Linq.Async is not referenced in this repo, so a
    /// LINQ <c>Select</c> over <see cref="IAsyncEnumerable{T}"/> is not available — this local
    /// adapter is the manual equivalent.
    /// </summary>
    private static async IAsyncEnumerable<string> ContentOf(
        IAsyncEnumerable<AiStreamChunk> chunks,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var c in chunks.WithCancellation(ct))
            yield return c.Content;
    }
}
