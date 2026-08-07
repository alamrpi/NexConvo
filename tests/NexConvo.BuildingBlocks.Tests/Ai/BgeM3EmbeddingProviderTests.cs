using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.BuildingBlocks.Ai.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.BuildingBlocks.Tests.Ai;

public class BgeM3EmbeddingProviderTests
{
    private const string QueryInstruction = "Represent this sentence for searching relevant passages: ";
    private const string BaseUrl = "http://test-bge:7997";

    // OpenAI-compatible embeddings response: data[].embedding, in request order.
    private static string ResponseFor(params float[][] embeddings)
    {
        var data = embeddings.Select(e => new { embedding = e });
        return JsonSerializer.Serialize(new { data, usage = new { total_tokens = 7 } });
    }

    private static float[] Vec(float seed) => Enumerable.Range(0, 1024).Select(i => seed + i).ToArray();

    private static BgeM3EmbeddingProviderService BuildProvider(CapturingJsonHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EMBEDDING:BGEM3:BASEURL"] = BaseUrl })
            .Build();

        return new BgeM3EmbeddingProviderService(
            new HttpClient(handler), configuration, NullLogger<BgeM3EmbeddingProviderService>.Instance);
    }

    [Fact]
    public void Dimensions_is_1024()
    {
        var provider = BuildProvider(new CapturingJsonHandler(ResponseFor(Vec(0))));

        provider.Dimensions.Should().Be(1024);
    }

    [Fact]
    public async Task Posts_to_embeddings_endpoint_with_model_and_input()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/embeddings");

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("model").GetString().Should().Be("BAAI/bge-m3");
        body.RootElement.GetProperty("input").ValueKind.Should().Be(JsonValueKind.Array);
        body.RootElement.GetProperty("input")[0].GetString().Should().Be("refund");
    }

    [Fact]
    public async Task Query_input_is_prefixed_with_the_retrieval_instruction()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        await provider.EmbedAsync("টাকা ফেরত", EmbeddingInputType.Query, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("input")[0].GetString()
            .Should().Be(QueryInstruction + "টাকা ফেরত");
    }

    [Fact]
    public async Task Document_input_is_left_raw()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        await provider.EmbedAsync("টাকা ফেরত", EmbeddingInputType.Document, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("input")[0].GetString().Should().Be("টাকা ফেরত");
        handler.LastBody!.Should().NotContain(QueryInstruction);
    }

    [Fact]
    public async Task Parses_a_1024_length_vector()
    {
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0)));
        var provider = BuildProvider(handler);

        var vector = await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        vector.Should().HaveCount(1024);
        vector[0].Should().Be(0f);
        vector[1023].Should().Be(1023f);
    }

    [Fact]
    public async Task Batch_preserves_input_order()
    {
        // Distinct vectors (seed 0 vs 100) let us prove the mapping is order-preserving.
        var handler = new CapturingJsonHandler(ResponseFor(Vec(0), Vec(100)));
        var provider = BuildProvider(handler);

        var vectors = await provider.EmbedBatchAsync(
            new[] { "first", "second" }, EmbeddingInputType.Document, CancellationToken.None);

        vectors.Should().HaveCount(2);
        vectors[0][0].Should().Be(0f);
        vectors[1][0].Should().Be(100f);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var input = body.RootElement.GetProperty("input");
        input[0].GetString().Should().Be("first");
        input[1].GetString().Should().Be("second");
    }

    [Fact]
    public async Task Throws_when_provider_returns_wrong_dimension()
    {
        var handler = new CapturingJsonHandler(ResponseFor(new float[] { 1f, 2f, 3f }));
        var provider = BuildProvider(handler);

        var act = async () => await provider.EmbedAsync("refund", EmbeddingInputType.Document, CancellationToken.None);

        await act.Should().ThrowAsync<System.InvalidOperationException>();
    }
}
