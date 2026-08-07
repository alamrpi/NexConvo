using System.Collections.Generic;
using NexConvo.BuildingBlocks.Ai.Models;

namespace NexConvo.Chat.Application.Features.Playground.Queries.GetProvidersAndModels;

public sealed record ProviderModelDto(string ProviderId, string ProviderName, IReadOnlyList<ModelDto> Models);
