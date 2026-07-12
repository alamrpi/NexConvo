using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

/// <summary>
/// TenantId travels with the command (not re-derived from an ambient HTTP tenant, which doesn't
/// exist here — this command is dispatched from a MassTransit consumer) so the handler can build
/// an RLS-scoped IChatDbContext before it ever needs to query for the conversation.
/// </summary>
public sealed record GenerateRagReplyCommand(Guid TenantId, Guid ConversationId) : IRequest<Result>;
