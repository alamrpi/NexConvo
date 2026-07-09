using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.BuildingBlocks.Ai.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.BuildingBlocks.Tests.Ai;

public class CohereEmbeddingProviderTests
{
    private const string ApiKey = "cohere-secret-key-abc123";

    // Cohere v2 /embed response: embeddings.float[][], in request order.
    private static string ResponseFor(params float[][] embeddings)
        => JsonSerializer.Serialize(new
        {
            embeddings = new { @float = embeddings },
            meta = new { billed_units = new { input_tokens = 5 } }
        });

    private static float[] Vec(float seed) => Enumerable.Range(0, 1024).Select(i => seed + i).ToArray();

    private static CohereEmbeddingProviderService BuildProvider(
        CapturingJsonHandler handler,
        Microsoft.Extensions.Logging.ILogger<CohereEmbeddingProviderService>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EMBEDDING:COHERE:APIKEY"] = ApiKey })
            .Build();

        return new CohereEmbeddingProviderService(
            new System.Net.Http.HttpClient(handler),
            configuration,
            logger ?? NullLogger<CohereEmbeddingProviderService>.Instance);
    }

    [Fact]
    public void Dimensions_is_1024()
    {
        var provider = BuildProvider(new CapturingJsonHandler(ResponseFor(Vec(0))));

        provider.Dimensions.Should().Be(1024);
    }

    [Fact]
    public async Task Posts_to_cohere_v2_embed_with_model_and_float_embedding_type()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.cohere.com/v2/embed");

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("model").GetString().Should().Be("embed-multilingual-v3.0");
        body.RootElement.GetProperty("embedding_types")[0].GetString().Should().Be("float");
        body.RootElement.GetProperty("texts")[0].GetString().Should().Be("refund");
    }

    [Theory]
    [InlineData(EmbeddingInputType.Query, "search_query")]
    [InlineData(EmbeddingInputType.Document, "search_document")]
    public async Task Maps_input_type_to_cohere_search_type(EmbeddingInputType inputType, string expected)
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        await provider.EmbedAsync("টাকা ফেরত", inputType, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("input_type").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task Sends_api_key_as_bearer_token()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be(ApiKey);
    }

    [Fact]
    public async Task Parses_1024_length_vectors_in_order()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0), Vec(100)));
        var provider = BuildProvider(handler);

        var vectors = await provider.EmbedBatchAsync(
            new[] { "first", "second" }, EmbeddingInputType.Document, CancellationToken.None);

        vectors.Should().HaveCount(2);
        vectors[0].Should().HaveCount(1024);
        vectors[0][0].Should().Be(0f);
        vectors[1][0].Should().Be(100f);
    }

    [Fact]
    public async Task Never_logs_the_api_key()
    {
        var logger = new CapturingLogger<CohereEmbeddingProviderService>();
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler, logger);

        await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        logger.All.Should().NotBeEmpty("the provider logs embedding usage");
        logger.All.Should().NotContain(m => m.Contains(ApiKey));
    }
}
