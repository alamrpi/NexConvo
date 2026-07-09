using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Contracts.Enums;
using System.Collections.Generic;
using Xunit;

namespace NexConvo.BuildingBlocks.Tests.Ai;

public class EmbeddingProviderFactoryDiTests
{
    private static IEmbeddingProviderFactory BuildFactory(string? provider)
    {
        var settings = new Dictionary<string, string?>();
        if (provider is not null)
            settings["EMBEDDING:PROVIDER"] = provider;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEmbeddingProviders(configuration);
        var sp = services.BuildServiceProvider();

        return sp.GetRequiredService<IEmbeddingProviderFactory>();
    }

    [Fact]
    public void Active_provider_defaults_to_bge_m3_when_unset()
    {
        var factory = BuildFactory(provider: null);

        factory.GetActiveProvider().Should().BeOfType<BgeM3EmbeddingProviderService>();
    }

    [Fact]
    public void Active_provider_switches_to_cohere_via_config()
    {
        var factory = BuildFactory("Cohere");

        factory.GetActiveProvider().Should().BeOfType<CohereEmbeddingProviderService>();
    }

    [Fact]
    public void Active_provider_selection_is_case_insensitive()
    {
        var factory = BuildFactory("cohere");

        factory.GetActiveProvider().Should().BeOfType<CohereEmbeddingProviderService>();
    }

    [Fact]
    public void Unknown_provider_name_throws()
    {
        var factory = BuildFactory("Llama");

        var act = () => factory.GetActiveProvider();

        act.Should().Throw<System.ArgumentException>();
    }

    [Theory]
    [InlineData(EmbeddingProviderType.BgeM3, typeof(BgeM3EmbeddingProviderService))]
    [InlineData(EmbeddingProviderType.Cohere, typeof(CohereEmbeddingProviderService))]
    public void GetProvider_resolves_each_supported_type(EmbeddingProviderType type, System.Type expected)
    {
        var factory = BuildFactory(provider: null);

        factory.GetProvider(type).Should().BeOfType(expected);
    }

    [Fact]
    public void GetProvider_throws_for_unknown_enum_value()
    {
        var factory = BuildFactory(provider: null);

        var act = () => factory.GetProvider((EmbeddingProviderType)99);

        act.Should().Throw<System.NotSupportedException>();
    }
}
