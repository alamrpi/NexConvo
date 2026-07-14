using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Api.Realtime;

/// <summary>
/// The Chat channel's real-time fabric. Two group families:
/// <c>conv:{conversationId}</c> — the live message feed + token stream for one conversation;
/// <c>tenant:{tenantId}:agents</c> — the handoff queue every agent dashboard subscribes to.
///
/// Deny-by-default: the class-level [Authorize] rejects unauthenticated connections, tenant
/// identity comes exclusively from the JWT tenant_id claim (never a client argument), and
/// JoinConversation additionally requires the caller to be the assigned agent or hold the
/// conversations:read permission. Web-widget contacts have no auth yet and are therefore refused;
/// a future slice will admit them via a signed per-conversation token.
/// </summary>
[Authorize]
public sealed class ChatHub(
    IChatDbContextFactory dbFactory,
    ILogger<ChatHub> logger) : Hub
{
    public const string ClientMethod = "chatEvent";

    // Same message for "doesn't exist" and "belongs to another tenant" — a refusal must never
    // reveal whether a conversation id exists outside the caller's tenant.
    private const string AccessDeniedMessage = "Conversation not found or access denied.";

    public static string ConversationGroup(Guid conversationId) => $"conv:{conversationId}";

    public static string AgentsGroup(Guid tenantId) => $"tenant:{tenantId}:agents";

    public async Task JoinConversation(Guid conversationId)
    {
        var tenantId = GetRequiredTenantId();

        // Tenant-pinned context: RLS makes other tenants' rows invisible; the explicit TenantId
        // predicate is belt-and-braces on top.
        await using var db = dbFactory.CreateForTenant(tenantId);
        var conversation = await db.Conversations.FirstOrDefaultAsync(
            c => c.Id == conversationId && c.TenantId == tenantId, Context.ConnectionAborted);
        if (conversation is null)
        {
            throw new HubException(AccessDeniedMessage);
        }

        var isAssignedAgent = GetUserId() is { } userId && conversation.AssignedAgentUserId == userId;
        if (!isAssignedAgent && !HasConversationsReadPermission())
        {
            throw new HubException(AccessDeniedMessage);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId), Context.ConnectionAborted);
        logger.LogInformation("Connection joined conversation group for {ConversationId}", conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        // Leaving needs no membership check — a connection can only ever remove itself.
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId), Context.ConnectionAborted);
    }

    [Authorize(Policy = "conversations:read")]
    public async Task JoinAgentDashboard()
    {
        var tenantId = GetRequiredTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, AgentsGroup(tenantId), Context.ConnectionAborted);
        logger.LogInformation("Connection joined agents group for tenant {TenantId}", tenantId);
    }

    private Guid GetRequiredTenantId()
    {
        var claim = Context.User?.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        return Guid.TryParse(claim, out var tenantId)
            ? tenantId
            : throw new HubException(AccessDeniedMessage);
    }

    private Guid? GetUserId()
    {
        var claim = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private bool HasConversationsReadPermission() =>
        Context.User?.HasClaim("permission", "*") == true
        || Context.User?.HasClaim("permission", "conversations:read") == true;
}
