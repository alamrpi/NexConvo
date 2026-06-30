using MediatR;
using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

public sealed record TestAiConnectionCommand(
    AiProviderType Provider,
    string? ApiKey,
    string? BaseUrl,
    string Model) : IRequest<TestAiConnectionResult>;

public sealed record TestAiConnectionResult(bool Success, string? Error);
