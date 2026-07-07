using FluentAssertions;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Features.ChannelConnections.Dtos;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using System;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests;

/// <summary>
/// Unit tests for the single pure ConnectionStatus → 3-value status string mapping used by both
/// SaveChannelConnectionCommandHandler and GetChannelConnectionsQueryHandler.
/// </summary>
public class ChannelConnectionDtoMapperTests
{
    private static ChannelConnection NewConnection() =>
        new(Guid.NewGuid(), ChatChannel.WhatsApp, "acct-1", "My Account", "ENC_TOKEN");

    [Fact]
    public void FromEntity_maps_Healthy_to_connected_with_no_error_message()
    {
        var connection = NewConnection();
        connection.ApplyHealth(ConnectionHealth.Healthy("ok", 10));

        var dto = ChannelConnectionDto.FromEntity(connection);

        dto.Status.Should().Be("connected");
        dto.ErrorMessage.Should().BeNull();
        dto.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
    }

    [Fact]
    public void FromEntity_maps_Untested_to_disconnected()
    {
        var connection = NewConnection();

        var dto = ChannelConnectionDto.FromEntity(connection);

        dto.Status.Should().Be("disconnected");
        dto.ErrorMessage.Should().BeNull();
        dto.LastTestStatus.Should().Be(ConnectionStatus.Untested);
    }

    [Fact]
    public void FromEntity_maps_Failed_to_error_with_error_message_set()
    {
        var connection = NewConnection();
        connection.ApplyHealth(ConnectionHealth.Failed("bad token", null));

        var dto = ChannelConnectionDto.FromEntity(connection);

        dto.Status.Should().Be("error");
        dto.ErrorMessage.Should().Be("bad token");
        dto.LastTestStatus.Should().Be(ConnectionStatus.Failed);
    }

    [Fact]
    public void FromEntity_maps_Degraded_to_error_with_error_message_set()
    {
        var connection = NewConnection();
        connection.ApplyHealth(new ConnectionHealth(true, ConnectionStatus.Degraded, "slow", "latency high", 900));

        var dto = ChannelConnectionDto.FromEntity(connection);

        dto.Status.Should().Be("error");
        dto.ErrorMessage.Should().Be("latency high");
        dto.LastTestStatus.Should().Be(ConnectionStatus.Degraded);
    }

    [Fact]
    public void FromEntity_maps_core_fields_from_entity()
    {
        var connection = NewConnection();

        var dto = ChannelConnectionDto.FromEntity(connection);

        dto.Id.Should().Be(connection.Id);
        dto.Channel.Should().Be(connection.Channel);
        dto.ExternalAccountId.Should().Be(connection.ExternalAccountId);
        dto.DisplayName.Should().Be(connection.AccountName);
        dto.IsActive.Should().Be(connection.IsActive);
        dto.CreatedAt.Should().Be(connection.CreatedAt);
        dto.MaskedAccessToken.Should().EndWith("OKEN");
    }
}
