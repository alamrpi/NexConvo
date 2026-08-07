using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using Xunit;

namespace NexConvo.Chat.Domain.Tests;

public class ChannelConnectionHealthTests
{
    private static ChannelConnection NewConnection() =>
        new(Guid.NewGuid(), ChatChannel.WhatsApp, "external-account-1", "Display Name", "enc-token");

    [Fact]
    public void ApplyHealth_healthy_sets_status_and_clears_error()
    {
        var c = NewConnection();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 30));
        Assert.Equal(ConnectionStatus.Healthy, c.LastTestStatus);
        Assert.Null(c.LastTestError);
        Assert.Equal(30, c.LastTestLatencyMs);
        Assert.NotNull(c.LastTestedAt);
    }

    [Fact]
    public void EnsureHealthy_throws_when_not_healthy()
    {
        var c = NewConnection(); // default Untested
        Assert.Throws<ConnectionUnhealthyException>(() => c.EnsureHealthy());
    }

    [Fact]
    public void EnsureHealthy_passes_when_healthy()
    {
        var c = NewConnection();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 10));
        c.EnsureHealthy(); // does not throw
    }
}
