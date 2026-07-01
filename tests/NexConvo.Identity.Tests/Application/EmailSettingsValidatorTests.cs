using FluentAssertions;
using NexConvo.Identity.Application.Settings.WorkspaceEmail;

namespace NexConvo.Identity.Tests.Application;

public sealed class EmailSettingsValidatorTests
{
    private static UpdateEmailSettingsCommand Cmd(
        string provider = "Smtp",
        string fromName = "Acme",
        string fromAddress = "no-reply@acme.com",
        string? smtpHost = "smtp.acme.com",
        int? smtpPort = 587) =>
        new(provider, fromName, fromAddress, true, smtpHost, smtpPort, null, true, null, Guid.NewGuid());

    [Fact]
    public void Valid_Smtp_Passes() =>
        new UpdateEmailSettingsCommandValidator().Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Valid_Resend_Passes() =>
        new UpdateEmailSettingsCommandValidator()
            .Validate(Cmd(provider: "Resend", smtpHost: null, smtpPort: null))
            .IsValid.Should().BeTrue();

    [Theory]
    [InlineData("Carrier-Pigeon", "no-reply@acme.com", "smtp.acme.com", 587)] // unknown provider
    [InlineData("Smtp", "not-an-email", "smtp.acme.com", 587)]                // bad from address
    [InlineData("Smtp", "no-reply@acme.com", null, 587)]                      // smtp host missing
    [InlineData("Smtp", "no-reply@acme.com", "smtp.acme.com", 70000)]         // port out of range
    public void Invalid_Inputs_Fail(string provider, string fromAddress, string? smtpHost, int? smtpPort) =>
        new UpdateEmailSettingsCommandValidator()
            .Validate(Cmd(provider: provider, fromAddress: fromAddress, smtpHost: smtpHost, smtpPort: smtpPort))
            .IsValid.Should().BeFalse();
}
