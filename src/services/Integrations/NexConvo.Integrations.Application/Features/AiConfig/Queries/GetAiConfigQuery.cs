using MediatR;
using System.Collections.Generic;

namespace NexConvo.Integrations.Application.Features.AiConfig.Queries;

/// <summary>Lists the current workspace's AI configs. Tenant is resolved from the JWT in the handler.</summary>
public sealed record GetAiConfigQuery : IRequest<List<AiConfigDto>>;
