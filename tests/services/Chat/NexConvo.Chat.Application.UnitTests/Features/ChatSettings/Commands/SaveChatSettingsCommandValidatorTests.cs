using FluentAssertions;
using FluentValidation.TestHelper;
using NexConvo.Chat.Application.Features.ChatSettings.Commands;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.UnitTests.Features.ChatSettings.Commands;

/// <summary>
/// Widget config is rendered on arbitrary public sites, so its inputs must be validated server-side
/// (audit H4): colors are hex, the icon URL is an https URL, and the welcome message is bounded.
/// </summary>
public class SaveChatSettingsCommandValidatorTests
{
    private readonly SaveChatSettingsCommandValidator _validator = new();

    private static SaveChatSettingsCommand Valid(
        string primaryColor = "#0F172A",
        string secondaryColor = "#3B82F6",
        string? iconUrl = null,
        string welcome = "Hi there! How can I help you today?",
        string noAnswer = "We don't have that info; please contact support.") =>
        new(
            AiProviderType.OpenAI, "gpt-4o-mini", FallbackProviders: [], SystemPromptOverride: null,
            HandoffConfidenceThreshold: 0.5, SentimentEscalationEnabled: false,
            SentimentSensitivity.Medium, TriggerPhrases: [], MaxUnansweredMessages: 3,
            PiiMaskingLevel.Off, DataRetentionDays: null,
            WidgetIconUrl: iconUrl, WidgetPrimaryColor: primaryColor,
            WidgetSecondaryColor: secondaryColor, WidgetWelcomeMessage: welcome,
            NoAnswerMessage: noAnswer,
            ActorUserId: Guid.NewGuid());

    [Theory]
    [InlineData("#0F172A")]
    [InlineData("#3B82F6")]
    [InlineData("#FFF")]
    [InlineData("#11223344")] // 8-digit hex + alpha
    public void ValidHexColor_Passes(string color)
    {
        _validator.TestValidate(Valid(primaryColor: color))
            .ShouldNotHaveValidationErrorFor(x => x.WidgetPrimaryColor);
    }

    [Theory]
    [InlineData("0F172A")]     // missing #
    [InlineData("#12345")]     // 5 digits
    [InlineData("red")]        // named color
    [InlineData("#GGGGGG")]    // non-hex chars
    [InlineData("")]           // empty
    public void InvalidHexColor_Fails(string color)
    {
        _validator.TestValidate(Valid(primaryColor: color))
            .ShouldHaveValidationErrorFor(x => x.WidgetPrimaryColor);
    }

    [Fact]
    public void SecondaryColor_IsValidatedToo()
    {
        _validator.TestValidate(Valid(secondaryColor: "not-a-color"))
            .ShouldHaveValidationErrorFor(x => x.WidgetSecondaryColor);
    }

    [Theory]
    [InlineData("https://example.com/icon.png")]
    [InlineData("https://cdn.example.com/a/b/c.svg")]
    public void ValidHttpsIconUrl_Passes(string url)
    {
        _validator.TestValidate(Valid(iconUrl: url))
            .ShouldNotHaveValidationErrorFor(x => x.WidgetIconUrl);
    }

    [Fact]
    public void NullIconUrl_Passes()
    {
        _validator.TestValidate(Valid(iconUrl: null))
            .ShouldNotHaveValidationErrorFor(x => x.WidgetIconUrl);
    }

    [Theory]
    [InlineData("http://example.com/icon.png")]        // not https
    [InlineData("javascript:alert(1)")]                // XSS scheme
    [InlineData("data:image/png;base64,AAAA")]         // data URL
    [InlineData("ftp://example.com/icon.png")]
    [InlineData("not a url")]
    public void NonHttpsOrMalformedIconUrl_Fails(string url)
    {
        _validator.TestValidate(Valid(iconUrl: url))
            .ShouldHaveValidationErrorFor(x => x.WidgetIconUrl);
    }

    [Fact]
    public void WelcomeMessage_Empty_Fails()
    {
        _validator.TestValidate(Valid(welcome: ""))
            .ShouldHaveValidationErrorFor(x => x.WidgetWelcomeMessage);
    }

    [Fact]
    public void WelcomeMessage_TooLong_Fails()
    {
        _validator.TestValidate(Valid(welcome: new string('a', 501)))
            .ShouldHaveValidationErrorFor(x => x.WidgetWelcomeMessage);
    }

    [Fact]
    public void NoAnswerMessage_Empty_Fails()
    {
        _validator.TestValidate(Valid(noAnswer: ""))
            .ShouldHaveValidationErrorFor(x => x.NoAnswerMessage);
    }

    [Fact]
    public void NoAnswerMessage_TooLong_Fails()
    {
        _validator.TestValidate(Valid(noAnswer: new string('a', 501)))
            .ShouldHaveValidationErrorFor(x => x.NoAnswerMessage);
    }

    [Fact]
    public void FullyValidWidgetConfig_HasNoErrors()
    {
        _validator.TestValidate(Valid(iconUrl: "https://example.com/i.png"))
            .IsValid.Should().BeTrue();
    }
}
