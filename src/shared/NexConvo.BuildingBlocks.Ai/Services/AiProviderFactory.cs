using Microsoft.Extensions.DependencyInjection;
using NexConvo.Contracts.Enums;
using System;
using System.Linq;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class AiProviderFactory : IAiProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AiProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IAiProviderService GetProvider(AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.OpenAI => _serviceProvider.GetRequiredKeyedService<IAiProviderService>("OpenAI"),
            AiProviderType.Anthropic => _serviceProvider.GetRequiredKeyedService<IAiProviderService>("Anthropic"),
            AiProviderType.Gemini => _serviceProvider.GetRequiredKeyedService<IAiProviderService>("Gemini"),
            AiProviderType.OpenRouter => _serviceProvider.GetRequiredKeyedService<IAiProviderService>("OpenRouter"),
            AiProviderType.DeepSeek => _serviceProvider.GetRequiredKeyedService<IAiProviderService>("DeepSeek"),
            _ => throw new NotSupportedException($"AI provider '{providerType}' is not supported.")
        };
    }

    public IAiProviderService GetProvider(string providerName)
    {
        if (Enum.TryParse<AiProviderType>(providerName, true, out var type))
        {
            return GetProvider(type);
        }
        
        throw new ArgumentException($"Invalid provider name: {providerName}", nameof(providerName));
    }
}
