using FluentAssertions;
using NexConvo.Chat.Application.Features.ChannelConnections.Commands;
using NexConvo.Chat.Domain.Enums;
using System;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests;

public class SaveChannelConnectionCommandValidatorTests
{
    private readonly SaveChannelConnectionCommandValidator _validator = new();

    private static SaveChannelConnectionCommand Cmd(
        ChatChannel channel = ChatChannel.WhatsApp,
        string externalAccountId = "acct-123",
        string accessToken = "token-abc",
        string? appSecret = null)
        => new(channel, externalAccountId, "Display Name", accessToken, appSecret, Guid.NewGuid());

    [Fact]
    public void Accepts_MinimalValidNonWebCommand()
        => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Rejects_OutOfRangeEnumValue()
        => _validator.Validate(Cmd(channel: (ChatChannel)99)).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_EmptyExternalAccountId_ForNonWebChannel()
        => _validator.Validate(Cmd(externalAccountId: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_EmptyAccessToken_ForNonWebChannel()
        => _validator.Validate(Cmd(accessToken: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Accepts_WebChannel_WithNoExternalAccountIdAndNoAccessToken()
        => _validator.Validate(Cmd(channel: ChatChannel.Web, externalAccountId: "", accessToken: ""))
            .IsValid.Should().BeTrue();

    [Fact]
    public void Rejects_AccessTokenLongerThan2000Chars_EvenForWebChannel()
        => _validator.Validate(Cmd(channel: ChatChannel.Web, externalAccountId: "", accessToken: new string('a', 2001)))
            .IsValid.Should().BeFalse();
}
