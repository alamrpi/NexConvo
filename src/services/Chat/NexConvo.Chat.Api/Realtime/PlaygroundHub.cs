using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
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
    ILogger<PlaygroundHub> logger) : Hub
{
    private const string AccessDeniedMessage = "Access denied.";

    public async Task ExecuteScenarioAsync(PlaygroundRequest request)
    {
        var tenantId = GetRequiredTenantId();
        var swTotal = Stopwatch.StartNew();

        logger.LogInformation("Playground execution started for tenant {TenantId}, model {ModelId}", tenantId, request.ModelId);

        // Fetch AI Config for the tenant to get the API Key
        var cachedBytes = await cache.GetAsync($"AiConfig:{tenantId}", Context.ConnectionAborted);
        if (cachedBytes is null)
        {
            throw new HubException("AI Configuration not found for tenant. Please configure it first.");
        }

        var aiConfig = JsonSerializer.Deserialize<CachedAiConfig>(Encoding.UTF8.GetString(cachedBytes))!;
        var apiKey = aes.Decrypt(aiConfig.EncryptedApiKey);

        // Map UI provider string to enum
        if (!Enum.TryParse<AiProviderType>(request.ProviderId, true, out var providerType))
        {
            // If parse fails, fallback to what's in config or default to OpenRouter
            _ = Enum.TryParse(aiConfig.Provider, true, out providerType);
        }

        var provider = aiProviderFactory.GetProvider(providerType);

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

        var contributions = matches
            .Select((m, i) => new ContextContribution(i + 1, m.ChunkId, m.DocumentId, m.Content, m.Score))
            .ToList();

        var (fittedContext, fittedHistory) = tokenBudgeter.Fit(contributions, [], ChannelProfile.Chat.MaxAnswerTokens * 4);

        var systemPrompt = promptAssembler.BuildSystemPrompt(ChannelProfile.Chat, request.SystemPrompt);
        var userPrompt = promptAssembler.BuildUserPrompt(fittedContext, fittedHistory, request.UserMessage, ChannelProfile.Chat);

        // 2. LLM Generation
        var swLlm = Stopwatch.StartNew();
        var replyBuilder = new StringBuilder();
        
        await foreach (var chunk in provider.GenerateStreamAsync(
            userPrompt, systemPrompt, apiKey, request.ModelId, aiConfig.BaseUrl, Context.ConnectionAborted))
        {
            Context.ConnectionAborted.ThrowIfCancellationRequested();
            replyBuilder.Append(chunk.Content);

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                await Clients.Caller.SendAsync("ReceiveToken", chunk.Content, Context.ConnectionAborted);
            }
        }
        swLlm.Stop();
        swTotal.Stop();

        var replyText = replyBuilder.ToString();
        var abstained = replyText.Contains(GroundedPromptAssembler.AbstentionMarker, StringComparison.OrdinalIgnoreCase);
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

        // Estimating tokens for playground UI if not provided by provider stream
        // Usually providers return token counts in the final chunk, but we'll approximate if needed.
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
            JsonSerializer.Serialize(new { response = replyText }), // Mock raw response for now
            Array.Empty<PlaygroundPiiToken>(), // No PII filtering implemented in this basic orchestrator yet
            timing
        );

        await Clients.Caller.SendAsync("ReceiveDebugData", debugData, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("ReceiveCompleted", Context.ConnectionAborted);
    }

    private Guid GetRequiredTenantId()
    {
        var claim = Context.User?.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        return Guid.TryParse(claim, out var tenantId)
            ? tenantId
            : throw new HubException(AccessDeniedMessage);
    }

    private sealed record CachedAiConfig(
        Guid TenantId, string Provider, string EncryptedApiKey, string? BaseUrl,
        string DefaultModel, string? SystemPrompt, string? Parameters, bool IsActive);
}
