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
/// Exercises the BGE-M3 provider against a live Infinity server. Skips automatically when the
/// server isn't running, so it never breaks a CI/offline run — start it with
/// <c>docker compose up -d bge-m3</c> to have these execute.
/// </summary>
public class EmbeddingLiveIntegrationTests
{
    private const string BaseUrl = "http://localhost:7997";

    // xUnit 2.x has no dynamic Assert.Skip, so this is a guarded (soft-skip) integration test:
    // when the Infinity server isn't running the test returns early instead of failing an
    // offline/CI run. It runs for real once `docker compose up -d bge-m3` is healthy.
    private static async Task<bool> ServerIsAvailable()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using var response = await client.GetAsync($"{BaseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static BgeM3EmbeddingProviderService BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EMBEDDING:BGEM3:BASEURL"] = BaseUrl })
            .Build();

        return new BgeM3EmbeddingProviderService(
            new HttpClient(), configuration, NullLogger<BgeM3EmbeddingProviderService>.Instance);
    }

    [Fact]
    public async Task Embeds_bengali_and_english_to_1024_dimensions()
    {
        if (!await ServerIsAvailable()) return;
        var provider = BuildProvider();

        var bengali = await provider.EmbedAsync("টাকা ফেরত", EmbeddingInputType.Query, CancellationToken.None);
        var english = await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        bengali.Should().HaveCount(1024);
        english.Should().HaveCount(1024);
    }

    [Fact]
    public async Task Batch_returns_one_vector_per_input_in_order()
    {
        if (!await ServerIsAvailable()) return;
        var provider = BuildProvider();

        var texts = new[] { "টাকা ফেরত", "refund", "delivery status" };
        var vectors = await provider.EmbedBatchAsync(texts, EmbeddingInputType.Document, CancellationToken.None);

        vectors.Should().HaveCount(3);
        vectors.Should().OnlyContain(v => v.Length == 1024);

        // Order preservation: embedding the same single text again matches the batch slot.
        var again = await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);
        again.Should().Equal(vectors[1]);
    }
}
