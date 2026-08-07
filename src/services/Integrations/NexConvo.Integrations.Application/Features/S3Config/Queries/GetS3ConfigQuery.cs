using MediatR;

namespace NexConvo.Integrations.Application.Features.S3Config.Queries;

/// <summary>Returns the workspace S3 config, or null if not yet configured. Tenant resolved from JWT.</summary>
public sealed record GetS3ConfigQuery : IRequest<S3ConfigDto?>;
