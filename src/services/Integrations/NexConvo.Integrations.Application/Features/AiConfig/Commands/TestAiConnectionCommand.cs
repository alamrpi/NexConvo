using MediatR;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

/// <summary>
/// Live "Test Connection" probe (Standard 22: test-then-save) for a workspace's AI provider.
/// When <see cref="ApiKey"/> is blank, the handler falls back to the tenant's stored, decrypted
/// key for this provider so the user can re-test without re-entering secrets.
/// </summary>
public sealed record TestAiConnectionCommand(
    AiProviderType Provider,
    string? ApiKey,
    string? BaseUrl,
    string Model) : IRequest<ConnectionHealth>;
