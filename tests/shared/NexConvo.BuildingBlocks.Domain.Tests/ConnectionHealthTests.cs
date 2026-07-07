using NexConvo.BuildingBlocks.Domain.Health;
using Xunit;

namespace NexConvo.BuildingBlocks.Domain.Tests.Unit;

public class ConnectionHealthTests
{
    [Fact]
    public void Healthy_factory_sets_success_and_status()
    {
        var h = ConnectionHealth.Healthy("Bucket reachable", 42);
        Assert.True(h.Success);
        Assert.Equal(ConnectionStatus.Healthy, h.Status);
        Assert.Equal("Bucket reachable", h.Detail);
        Assert.Null(h.ErrorMessage);
        Assert.Equal(42, h.LatencyMs);
    }

    [Fact]
    public void Failed_factory_sets_failure_and_error()
    {
        var h = ConnectionHealth.Failed("Access denied", 100);
        Assert.False(h.Success);
        Assert.Equal(ConnectionStatus.Failed, h.Status);
        Assert.Equal("Access denied", h.ErrorMessage);
        Assert.Null(h.Detail);
    }
}
