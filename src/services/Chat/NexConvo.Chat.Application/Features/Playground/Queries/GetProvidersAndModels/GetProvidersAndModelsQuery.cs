using System.Collections.Generic;
using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Playground.Queries.GetProvidersAndModels;

public sealed record GetProvidersAndModelsQuery(string? TenantId) : IRequest<Result<IReadOnlyList<ProviderModelDto>>>;
