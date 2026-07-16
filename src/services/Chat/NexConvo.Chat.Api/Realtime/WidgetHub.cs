using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Api.Realtime;

/// <summary>
/// Public-facing SignalR hub for web widget visitors.
///
/// Authentication: the caller supplies an unguessable public <c>token</c> (the tenant's
/// <c>WidgetToken</c>) as a query-string parameter — never the enumerable tenant id. On connect the
/// token is resolved to its owning tenant via <see cref="IWidgetTenantResolver"/> (the single
/// RLS-exempt lookup, which also requires an active Web channel); the resolved tenant is stashed on
/// the connection. Every subsequent read runs through <see cref="IChatDbContextFactory"/> so RLS is
/// actually enforced (Standard 6) — the hub never touches the DI-scoped, tenant-less DbContext.
///
/// No per-user identity is asserted — every widget visitor is anonymous. The hub relays user
/// messages through the same RAG → LLM pipeline as the Playground (without debug data) and streams
/// tokens back to the caller only.
/// </summary>
public sealed class WidgetHub(
    IWidgetTenantResolver tenantResolver,
    IChatDbContextFactory dbContextFactory,
    IKnowledgeRetrievalClient knowledge,
    IAiProviderFactory aiProviderFactory,
    IDistributedCache cache,
    IAesEncryptionService aes,
    IGroundedPromptAssembler promptAssembler,
    ITokenBudgeter tokenBudgeter,
    ILogger<WidgetHub> logger) : Hub
{
    // ─── Client method names ───────────────────────────────────────────────────
    public const string ReceiveToken     = "receiveToken";
    public const string ReceiveCompleted = "receiveCompleted";
    public const string ReceiveError     = "receiveError";

    // Key under which the resolved tenant is stored on the connection for its lifetime.
    private const string TenantItemKey = "widget.tenantId";

    // ─── Hub overrides ─────────────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var token = GetWidgetToken();
        var tenantId = token == Guid.Empty
            ? (Guid?)null
            : await tenantResolver.ResolveAsync(token, Context.ConnectionAborted);

        if (tenantId is null)
        {
            logger.LogWarning(
                "Widget connection rejected — unknown token or inactive Web channel. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            // HubException closes the connection cleanly without a 500. The message is deliberately
            // generic — it never reveals whether the token exists or the channel is inactive.
            throw new HubException("This chat widget is not available.");
        }

        Context.Items[TenantItemKey] = tenantId.Value;
        logger.LogInformation(
            "Widget visitor connected. Tenant={TenantId}, ConnectionId={ConnectionId}", tenantId.Value, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    // ─── Hub methods ───────────────────────────────────────────────────────────

    /// <summary>
    /// Streams an AI reply for the given user message.
    /// Emits one <c>receiveToken</c> per streamed chunk, then a final <c>receiveCompleted</c>.
    /// </summary>
    public async Task SendMessageAsync(string userMessage)
    {
        if (Context.Items[TenantItemKey] is not Guid tenantId)
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

        // 3. Load widget-specific system prompt override — via a tenant-scoped context so RLS applies.
        string? systemPromptOverride;
        await using (var db = dbContextFactory.CreateForTenant(tenantId))
        {
            var chatSettings = await db.WorkspaceChatSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, Context.ConnectionAborted);
            systemPromptOverride = chatSettings?.SystemPromptOverride;
        }

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

    /// <summary>
    /// The public widget token from the <c>?token=</c> query parameter (SignalR strips it from the
    /// negotiate/connect URL). Returns <see cref="Guid.Empty"/> if absent or malformed.
    /// </summary>
    private Guid GetWidgetToken()
    {
        var raw = Context.GetHttpContext()?.Request.Query["token"].ToString();
        return Guid.TryParse(raw, out var token) ? token : Guid.Empty;
    }
}
