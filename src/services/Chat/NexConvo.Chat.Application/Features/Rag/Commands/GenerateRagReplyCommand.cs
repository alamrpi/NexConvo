using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

public sealed record GenerateRagReplyCommand(Guid ConversationId) : IRequest<Result>;
