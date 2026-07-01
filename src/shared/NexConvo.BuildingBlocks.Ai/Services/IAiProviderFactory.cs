using NexConvo.Contracts.Enums;
using System;

namespace NexConvo.BuildingBlocks.Ai.Services;

public interface IAiProviderFactory
{
    IAiProviderService GetProvider(AiProviderType providerType);
    IAiProviderService GetProvider(string providerName);
}
