using MediatR;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Rag;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

public sealed class GenerateRagReplyCommandHandler(
    IReplyOrchestrator orchestrator,
    ILogger<GenerateRagReplyCommandHandler> logger) : IRequestHandler<GenerateRagReplyCommand, Result>
{
    public async Task<Result> Handle(GenerateRagReplyCommand request, CancellationToken cancellationToken)
    {
        var outcome = await orchestrator.RunAsync(request.ConversationId, cancellationToken);

        return outcome switch
        {
            AnsweredOutcome answered => LogAndSucceed(answered),
            HandoffOutcome handoff => LogAndSucceed(handoff),
            _ => Result.Failure("Unrecognized reply outcome."),
        };
    }

    private Result LogAndSucceed(AnsweredOutcome answered)
    {
        logger.LogInformation("RAG reply answered for message {MessageId}", answered.MessageId);
        return Result.Success();
    }

    private Result LogAndSucceed(HandoffOutcome handoff)
    {
        logger.LogInformation("RAG reply handed off, reason {Reason}", handoff.Reason);
        return Result.Success();
    }
}
