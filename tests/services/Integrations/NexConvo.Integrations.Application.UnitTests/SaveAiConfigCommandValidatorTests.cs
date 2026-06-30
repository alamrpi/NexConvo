using FluentAssertions;
using NexConvo.Contracts.Enums;
using NexConvo.Integrations.Application.Features.AiConfig.Commands;
using System;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class SaveAiConfigCommandValidatorTests
{
    private readonly SaveAiConfigCommandValidator _validator = new();

    private static SaveAiConfigCommand Cmd(
        AiProviderType provider = AiProviderType.OpenAI,
        string model = "gpt-4o",
        string? baseUrl = null,
        string? parameters = null)
        => new(provider, "key", baseUrl, model, parameters, true, Guid.NewGuid());

    [Fact]
    public void Accepts_MinimalValidCommand()
        => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.Anthropic)]
    [InlineData(AiProviderType.Gemini)]
    [InlineData(AiProviderType.OpenRouter)]
    [InlineData(AiProviderType.DeepSeek)]
    public void Accepts_AllSupportedProviders(AiProviderType provider)
        => _validator.Validate(Cmd(provider: provider)).IsValid.Should().BeTrue();

    [Fact]
    public void Rejects_OutOfRangeEnumValue()
        => _validator.Validate(Cmd(provider: (AiProviderType)99)).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_EmptyModel()
        => _validator.Validate(Cmd(model: "")).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("http://api.openai.com/v1")]       // not https
    [InlineData("https://localhost/v1")]           // loopback host
    [InlineData("https://169.254.169.254/latest")] // link-local cloud metadata
    [InlineData("https://10.0.0.5/v1")]            // RFC1918 private
    [InlineData("https://192.168.1.10/v1")]        // RFC1918 private
    [InlineData("not-a-url")]
    public void Rejects_UnsafeBaseUrl(string baseUrl)
        => _validator.Validate(Cmd(baseUrl: baseUrl)).IsValid.Should().BeFalse();

    [Fact]
    public void Accepts_PublicHttpsBaseUrl()
        => _validator.Validate(Cmd(baseUrl: "https://api.openai.com/v1")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")] // valid JSON but an array, not an object
    public void Rejects_NonObjectParameters(string parameters)
        => _validator.Validate(Cmd(parameters: parameters)).IsValid.Should().BeFalse();

    [Fact]
    public void Accepts_JsonObjectParameters()
        => _validator.Validate(Cmd(parameters: "{\"temperature\":0.7}")).IsValid.Should().BeTrue();
}
