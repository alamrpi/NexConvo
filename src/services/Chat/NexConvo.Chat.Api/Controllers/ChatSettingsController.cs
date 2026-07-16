using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Chat.Api.Extensions;
using NexConvo.Chat.Api.Models;
using NexConvo.Chat.Application.Features.ChatSettings.Commands;
using NexConvo.Chat.Application.Features.ChatSettings.Queries;

namespace NexConvo.Chat.Api.Controllers;

[ApiController]
[Route("api/v1/chat-settings")]
[Authorize(Policy = "settings:manage")]
public sealed class ChatSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await sender.Send(new GetChatSettingsQuery(), ct));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveChatSettingsRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var actor)) return Unauthorized();

        var result = await sender.Send(
            new SaveChatSettingsCommand(
                body.PrimaryProvider,
                body.PrimaryModel,
                body.FallbackProviders,
                body.SystemPromptOverride,
                body.HandoffConfidenceThreshold,
                body.SentimentEscalationEnabled,
                body.SentimentSensitivity,
                body.TriggerPhrases,
                body.MaxUnansweredMessages,
                body.PiiMaskingLevel,
                body.DataRetentionDays,
                body.WidgetIconUrl,
                body.WidgetPrimaryColor,
                body.WidgetSecondaryColor,
                body.WidgetWelcomeMessage,
                body.NoAnswerMessage,
                actor),
            ct);

        return result.ToActionResult();
    }

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}
