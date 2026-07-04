using MediatR;
using NexConvo.Chat.Application.Features.ChatSettings.Dtos;

namespace NexConvo.Chat.Application.Features.ChatSettings.Queries;

/// <summary>Returns the current tenant's chatbot settings, or sensible defaults if none have been saved.</summary>
public sealed record GetChatSettingsQuery : IRequest<WorkspaceChatSettingsDto>;
