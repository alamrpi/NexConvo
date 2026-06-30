using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Contracts.Enums;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.BuildingBlocks.Tests.Ai;

// ──────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────

/// <summary>
/// Fake handler that returns a canned SSE body and optionally captures the outgoing request.
/// </summary>
file sealed class FakeHttpMessageHandler(string sseBody) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseBody, Encoding.UTF8, "text/event-stream")
        };
        return Task.FromResult(response);
    }
}

// ──────────────────────────────────────────────────────────
// OpenAI-compatible SSE parsing (shared base covers OpenAI, OpenRouter, DeepSeek)
// ──────────────────────────────────────────────────────────

public class OpenAiCompatibleSseParseTests
{
    private const string SseBody =
        "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}\n\n" +
        "data: {\"choices\":[{\"delta\":{\"content\":\" world\"},\"finish_reason\":null}]}\n\n" +
        "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
        "data: [DONE]\n\n";

    [Fact]
    public async Task Parses_chunks_and_finish_reason()
    {
        var handler = new FakeHttpMessageHandler(SseBody);
        var service = new OpenAiProviderService(new HttpClient(handler));

        var chunks = new System.Collections.Generic.List<NexConvo.BuildingBlocks.Ai.Models.AiStreamChunk>();
        await foreach (var chunk in service.GenerateStreamAsync("hi", null, "sk-test", "gpt-4o"))
            chunks.Add(chunk);

        chunks.Should().HaveCount(3);
        chunks[0].Content.Should().Be("Hello");
        chunks[1].Content.Should().Be(" world");
        chunks[2].Reason.Should().Be("stop");
    }
}

// ──────────────────────────────────────────────────────────
// OpenRouter — correct endpoint + attribution headers
// ──────────────────────────────────────────────────────────

public class OpenRouterProviderServiceTests
{
    private const string MinimalSse = "data: [DONE]\n\n";

    [Fact]
    public async Task Hits_openrouter_endpoint_with_attribution_headers()
    {
        var handler = new FakeHttpMessageHandler(MinimalSse);
        var service = new OpenRouterProviderService(new HttpClient(handler));

        await foreach (var _ in service.GenerateStreamAsync("hi", null, "sk-test", "openai/gpt-4o")) { }

        handler.LastRequest!.RequestUri!.ToString()
            .Should().StartWith("https://openrouter.ai/api/v1/chat/completions");

        handler.LastRequest.Headers.TryGetValues("HTTP-Referer", out var referer).Should().BeTrue();
        referer.Should().Contain("https://nexconvo.app");

        handler.LastRequest.Headers.TryGetValues("X-Title", out var title).Should().BeTrue();
        title.Should().Contain("NexConvo");
    }
}

// ──────────────────────────────────────────────────────────
// DeepSeek — correct endpoint
// ──────────────────────────────────────────────────────────

public class DeepSeekProviderServiceTests
{
    private const string MinimalSse = "data: [DONE]\n\n";

    [Fact]
    public async Task Hits_deepseek_endpoint_with_bearer_auth()
    {
        var handler = new FakeHttpMessageHandler(MinimalSse);
        var service = new DeepSeekProviderService(new HttpClient(handler));

        await foreach (var _ in service.GenerateStreamAsync("hi", null, "sk-ds-test", "deepseek-chat")) { }

        handler.LastRequest!.RequestUri!.ToString()
            .Should().StartWith("https://api.deepseek.com/v1/chat/completions");

        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("sk-ds-test");
    }
}

// ──────────────────────────────────────────────────────────
// Gemini SSE parsing + x-goog-api-key header
// ──────────────────────────────────────────────────────────

public class GeminiProviderServiceTests
{
    private const string SseBody =
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Bonjour\"}]}}]}\n\n" +
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\" monde\"}]},\"finishReason\":\"STOP\"}]}\n\n";

    [Fact]
    public async Task Parses_gemini_chunks_and_sets_api_key_header()
    {
        var handler = new FakeHttpMessageHandler(SseBody);
        var service = new GeminiProviderService(new HttpClient(handler));

        var chunks = new System.Collections.Generic.List<NexConvo.BuildingBlocks.Ai.Models.AiStreamChunk>();
        await foreach (var chunk in service.GenerateStreamAsync("hi", null, "gemini-key", "gemini-2.0-flash"))
            chunks.Add(chunk);

        chunks.Should().HaveCount(2);
        chunks[0].Content.Should().Be("Bonjour");
        chunks[1].Content.Should().Be(" monde");
        chunks[1].Reason.Should().Be("STOP");

        handler.LastRequest!.Headers.TryGetValues("x-goog-api-key", out var key).Should().BeTrue();
        key.Should().Contain("gemini-key");
    }

    [Fact]
    public async Task Uses_model_in_url_path()
    {
        var handler = new FakeHttpMessageHandler("data: {\"candidates\":[]}\n\n");
        var service = new GeminiProviderService(new HttpClient(handler));

        await foreach (var _ in service.GenerateStreamAsync("hi", null, "key", "gemini-2.0-flash")) { }

        handler.LastRequest!.RequestUri!.ToString()
            .Should().Contain("/models/gemini-2.0-flash:streamGenerateContent?alt=sse");
    }
}

// ──────────────────────────────────────────────────────────
// Factory / DI smoke — all 5 providers resolve
// ──────────────────────────────────────────────────────────

public class AiProviderFactoryDiTests
{
    [Theory]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.Anthropic)]
    [InlineData(AiProviderType.Gemini)]
    [InlineData(AiProviderType.OpenRouter)]
    [InlineData(AiProviderType.DeepSeek)]
    public void GetProvider_returns_non_null_for_all_supported_types(AiProviderType type)
    {
        var services = new ServiceCollection();
        services.AddAiProviders();
        var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IAiProviderFactory>();
        var provider = factory.GetProvider(type);

        provider.Should().NotBeNull();
    }

    [Fact]
    public void GetProvider_throws_for_unknown_enum_value()
    {
        var services = new ServiceCollection();
        services.AddAiProviders();
        var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IAiProviderFactory>();

        var act = () => factory.GetProvider((AiProviderType)99);
        act.Should().Throw<System.NotSupportedException>();
    }
}
