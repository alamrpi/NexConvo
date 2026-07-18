using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Chat.Api.Extensions;
using NexConvo.Chat.Application.Features.Conversations.Commands;
using NexConvo.Chat.Application.Features.Conversations.Queries;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Exceptions;

namespace NexConvo.Chat.Api.Controllers;

/// <summary>
/// Agent-facing REST surface for the inbox (add-chat-inbox): list conversations, read message
/// history, reply, and change conversation state. Reuses the existing Conversation domain state
/// machine via MediatR commands — this controller only does auth/actor-extraction/error-mapping.
///
/// "conversations:read" gates the two read endpoints; "conversations:write" gates the four
/// mutating endpoints (reply/take-over/resolve/reopen), mirroring the read policy's
/// RequirePermission("*") wildcard semantics.
/// </summary>
[ApiController]
[Route("api/v1/conversations")]
[Authorize]
public sealed class ConversationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "conversations:read")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] ConversationState? state,
        [FromQuery] string? channel,
        [FromQuery] bool assignedToMe,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var agentUserId)) return Unauthorized();

        var result = await sender.Send(
            new GetConversationsQuery(
                state, channel, assignedToMe, agentUserId, cursor,
                pageSize > 0 ? pageSize : 25),
            ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}/messages")]
    [Authorize(Policy = "conversations:read")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var agentUserId)) return Unauthorized();

        // "conversations:read" already gates this action, so the query's own authorization check
        // (assigned agent OR has-read-permission) always short-circuits true for anyone reaching
        // this line — the query still accepts the flag so it can also be called for narrower
        // "assigned agent only" scenarios in the future without a signature change.
        var result = await sender.Send(
            new GetConversationMessagesQuery(
                id, agentUserId, RequestingAgentHasReadPermission: true, cursor,
                pageSize > 0 ? pageSize : 50),
            ct);

        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/reply")]
    [Authorize(Policy = "conversations:write")]
    public async Task<IActionResult> SendReply(Guid id, [FromBody] SendReplyRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var agentUserId)) return Unauthorized();

        try
        {
            var result = await sender.Send(new SendAgentReplyCommand(id, agentUserId, body.Text), ct);
            return result.ToActionResult();
        }
        catch (InvalidConversationStateTransitionException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/take-over")]
    [Authorize(Policy = "conversations:write")]
    public async Task<IActionResult> TakeOver(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var agentUserId)) return Unauthorized();

        try
        {
            var result = await sender.Send(new TakeOverConversationCommand(id, agentUserId), ct);
            return result.ToActionResult();
        }
        catch (InvalidConversationStateTransitionException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "conversations:write")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized();

        try
        {
            var result = await sender.Send(new ResolveConversationCommand(id, actorUserId), ct);
            return result.ToActionResult();
        }
        catch (InvalidConversationStateTransitionException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = "conversations:write")]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized();

        try
        {
            var result = await sender.Send(new ReopenConversationCommand(id, actorUserId), ct);
            return result.ToActionResult();
        }
        catch (InvalidConversationStateTransitionException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record SendReplyRequest(string Text);
