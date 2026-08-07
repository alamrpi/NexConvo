using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Features.Playground.Queries.GetProvidersAndModels;

public sealed class GetProvidersAndModelsQueryHandler : IRequestHandler<GetProvidersAndModelsQuery, Result<IReadOnlyList<ProviderModelDto>>>
{
    private readonly IDistributedCache _cache;
    private readonly IAesEncryptionService _aes;
    private readonly IAiProviderFactory _aiProviderFactory;

    public GetProvidersAndModelsQueryHandler(
        IDistributedCache cache,
        IAesEncryptionService aes,
        IAiProviderFactory aiProviderFactory)
    {
        _cache = cache;
        _aes = aes;
        _aiProviderFactory = aiProviderFactory;
    }

    public async Task<Result<IReadOnlyList<ProviderModelDto>>> Handle(GetProvidersAndModelsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.TenantId))
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure("Tenant ID is missing.");
        }

        var cachedBytes = await _cache.GetAsync($"AiConfig:{request.TenantId}", cancellationToken);
        if (cachedBytes == null)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure("AI Configuration not found for tenant. Please configure it first.");
        }

        var aiConfig = JsonSerializer.Deserialize<CachedAiConfig>(Encoding.UTF8.GetString(cachedBytes))!;
        var apiKey = _aes.Decrypt(aiConfig.EncryptedApiKey);

        if (!Enum.TryParse<AiProviderType>(aiConfig.Provider, true, out var providerType))
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure("Invalid AI provider configured.");
        }

        var providerService = _aiProviderFactory.GetProvider(providerType);
        var dynamicModels = await providerService.GetAvailableModelsAsync(apiKey, aiConfig.BaseUrl, cancellationToken);

        var providers = new List<ProviderModelDto>
        {
            new ProviderModelDto(providerType.ToString().ToLowerInvariant(), providerType.ToString(), dynamicModels)
        };

        return Result<IReadOnlyList<ProviderModelDto>>.Success(providers);
    }
}
