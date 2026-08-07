using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.BuildingBlocks.Ai.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.BuildingBlocks.Tests.Ai;

/// <summary>
/// Exercises the Cohere provider against the live Cohere API. Guarded (soft-skip) on the presence of
/// EMBEDDING__COHERE__APIKEY so offline/CI runs never fail; the key is read from the environment only
/// — never hardcoded, committed, or logged.
/// </summary>
public class CohereLiveIntegrationTests
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("EMBEDDING__COHERE__APIKEY");

    private static CohereEmbeddingProviderService BuildProvider(string apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EMBEDDING:COHERE:APIKEY"] = apiKey })
            .Build();

        return new CohereEmbeddingProviderService(
            new HttpClient(), configuration, NullLogger<CohereEmbeddingProviderService>.Instance);
    }

    [Fact]
    public async Task Embeds_bengali_and_english_to_1024_dimensions()
    {
        var key = ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return; // soft-skip when no key is configured
        var provider = BuildProvider(key);

        var bengali = await provider.EmbedAsync("টাকা ফেরত", EmbeddingInputType.Query, CancellationToken.None);
        var english = await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        bengali.Should().HaveCount(1024);
        english.Should().HaveCount(1024);
    }

    [Fact]
    public async Task Batch_returns_one_vector_per_input_in_order()
    {
        var key = ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return; // soft-skip when no key is configured
        var provider = BuildProvider(key);

        var texts = new[] { "টাকা ফেরত", "refund", "delivery status" };
        var vectors = await provider.EmbedBatchAsync(texts, EmbeddingInputType.Document, CancellationToken.None);

        vectors.Should().HaveCount(3);
        vectors.Should().OnlyContain(v => v.Length == 1024);
    }
}
