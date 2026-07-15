using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Api.Realtime;

/// <summary>
/// Public-facing SignalR hub for web widget visitors.
/// Authentication: the caller supplies their <c>tenant_id</c> as a query-string
/// parameter (no JWT required), which is validated and injected as a synthetic
/// ClaimsPrincipal so the rest of the pipeline sees a normal tenant context.
///
/// No per-user identity is asserted — every widget visitor is anonymous.
/// The hub relays user messages through the same RAG → LLM pipeline used by the
/// Playground but without debug data, and streams tokens back to the caller only.
/// </summary>
public sealed class WidgetHub(
    IKnowledgeRetrievalClient knowledge,
    IAiProviderFactory aiProviderFactory,
    IDistributedCache cache,
    IAesEncryptionService aes,
    IGroundedPromptAssembler promptAssembler,
    ITokenBudgeter tokenBudgeter,
    IChatDbContext db,
    ILogger<WidgetHub> logger) : Hub
{
    // ─── Client method names ───────────────────────────────────────────────────
    public const string ReceiveToken     = "receiveToken";
    public const string ReceiveCompleted = "receiveCompleted";
    public const string ReceiveError     = "receiveError";

    // ─── Hub overrides ─────────────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning("Widget connection rejected — missing or invalid tenant_id. ConnectionId={ConnectionId}", Context.ConnectionId);
            // HubException closes the connection cleanly without a 500
            throw new HubException("Invalid or missing tenant identifier.");
        }

        // Enforce that the Web Widget channel must be connected and active for this tenant
        var isChannelConnected = await db.ChannelConnections.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Channel == ChatChannel.Web && x.IsActive);

        if (!isChannelConnected)
        {
            logger.LogWarning("Widget connection rejected — Web channel not connected/active. Tenant={TenantId}, ConnectionId={ConnectionId}", tenantId, Context.ConnectionId);
            throw new HubException("Web widget channel is not active or connected for this workspace.");
        }

        logger.LogInformation("Widget visitor connected. Tenant={TenantId}, ConnectionId={ConnectionId}", tenantId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    // ─── Hub methods ───────────────────────────────────────────────────────────

    /// <summary>
    /// Streams an AI reply for the given user message.
    /// Emits one <c>receiveToken</c> per streamed chunk, then a final <c>receiveCompleted</c>.
    /// </summary>
    public async Task SendMessageAsync(string userMessage)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
        {
            await Clients.Caller.SendAsync(ReceiveError, "Tenant not identified.", Context.ConnectionAborted);
            return;
        }

        if (string.IsNullOrWhiteSpace(userMessage) || userMessage.Length > 4000)
        {
            await Clients.Caller.SendAsync(ReceiveError, "Message is empty or too long.", Context.ConnectionAborted);
            return;
        }

        logger.LogInformation("Widget message received. Tenant={TenantId}, Length={Len}", tenantId, userMessage.Length);

        // 1. Load AI config from cache (same as PlaygroundHub)
        var cachedBytes = await cache.GetAsync($"AiConfig:{tenantId}", Context.ConnectionAborted);
        if (cachedBytes is null)
        {
            await Clients.Caller.SendAsync(ReceiveError, "This chat widget is not fully configured yet. Please try again later.", Context.ConnectionAborted);
            return;
        }

        var aiConfig = JsonSerializer.Deserialize<CachedAiConfig>(Encoding.UTF8.GetString(cachedBytes))!;

        if (!aiConfig.IsActive)
        {
            await Clients.Caller.SendAsync(ReceiveError, "AI assistant is currently disabled.", Context.ConnectionAborted);
            return;
        }

        // 2. Decrypt API key
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

        // 3. Load widget-specific system prompt override if available
        var chatSettings = await db.WorkspaceChatSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, Context.ConnectionAborted);
        var systemPromptOverride = chatSettings?.SystemPromptOverride;

        // 4. Resolve provider
        if (!Enum.TryParse<AiProviderType>(aiConfig.Provider, true, out var providerType))
            providerType = AiProviderType.OpenRouter;

        var provider = aiProviderFactory.GetProvider(providerType);

        // 5. Retrieval
        var matches = await knowledge.SearchAsync(
            tenantId, userMessage, ChannelProfile.Chat.TopK, ChannelProfile.Chat.MinScore, Context.ConnectionAborted);

        var contributions = matches
            .Select((m, i) => new ContextContribution(i + 1, m.ChunkId, m.DocumentId, m.Content, m.Score))
            .ToList();

        var (fittedContext, _) = tokenBudgeter.Fit(contributions, [], ChannelProfile.Chat.MaxAnswerTokens * 4);

        var systemPrompt = promptAssembler.BuildSystemPrompt(ChannelProfile.Chat, systemPromptOverride);
        var userPrompt   = promptAssembler.BuildUserPrompt(fittedContext, [], userMessage, ChannelProfile.Chat);

        // 6. Stream LLM tokens to the caller only
        try
        {
            await foreach (var chunk in provider.GenerateStreamAsync(
                userPrompt, systemPrompt, apiKey, aiConfig.DefaultModel, aiConfig.BaseUrl, Context.ConnectionAborted))
            {
                Context.ConnectionAborted.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    await Clients.Caller.SendAsync(ReceiveToken, chunk.Content, Context.ConnectionAborted);
                }
            }

            await Clients.Caller.SendAsync(ReceiveCompleted, Context.ConnectionAborted);
            logger.LogInformation("Widget response streamed successfully for tenant {TenantId}", tenantId);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Widget stream cancelled by client. Tenant={TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming AI response for widget. Tenant={TenantId}", tenantId);
            await Clients.Caller.SendAsync(ReceiveError, "An error occurred while generating a response.", Context.ConnectionAborted);
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private Guid GetTenantId()
    {
        // The tenant_id claim is set by the WidgetHubMiddleware/filter via the
        // query-string parameter before OnConnectedAsync fires.
        var claim = Context.User?.FindFirst("tenant_id")?.Value
                 ?? Context.GetHttpContext()?.Request.Query["tenantId"].ToString();
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private sealed record CachedAiConfig(
        Guid TenantId, string Provider, string EncryptedApiKey, string? BaseUrl,
        string DefaultModel, string? SystemPrompt, string? Parameters, bool IsActive);
}
