using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.Contracts.Enums;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public EmbeddingProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public IEmbeddingProviderService GetActiveProvider()
    {
        var providerName = _configuration["EMBEDDING:PROVIDER"];
        if (string.IsNullOrWhiteSpace(providerName))
        {
            providerName = nameof(EmbeddingProviderType.BgeM3);
        }

        return GetProvider(providerName);
    }

    public IEmbeddingProviderService GetProvider(EmbeddingProviderType providerType)
    {
        return providerType switch
        {
            EmbeddingProviderType.BgeM3 => _serviceProvider.GetRequiredKeyedService<IEmbeddingProviderService>("BgeM3"),
            EmbeddingProviderType.Cohere => _serviceProvider.GetRequiredKeyedService<IEmbeddingProviderService>("Cohere"),
            _ => throw new NotSupportedException($"Embedding provider '{providerType}' is not supported.")
        };
    }

    public IEmbeddingProviderService GetProvider(string providerName)
    {
        if (Enum.TryParse<EmbeddingProviderType>(providerName, true, out var type))
        {
            return GetProvider(type);
        }

        throw new ArgumentException($"Invalid embedding provider name: {providerName}", nameof(providerName));
    }
}
