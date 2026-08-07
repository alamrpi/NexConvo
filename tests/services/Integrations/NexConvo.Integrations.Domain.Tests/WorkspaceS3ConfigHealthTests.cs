using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Integrations.Domain.Entities;
using Xunit;

namespace NexConvo.Integrations.Domain.Tests;

public class WorkspaceS3ConfigHealthTests
{
    private static WorkspaceS3Config NewConfig() =>
        new(Guid.NewGuid(), "bucket", "us-east-1", "enc-ak", "enc-sk", null, null, true);

    [Fact]
    public void ApplyHealth_healthy_sets_status_and_clears_error()
    {
        var c = NewConfig();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 30));
        Assert.Equal(ConnectionStatus.Healthy, c.LastTestStatus);
        Assert.Null(c.LastTestError);
        Assert.Equal(30, c.LastTestLatencyMs);
        Assert.NotNull(c.LastTestedAt);
    }

    [Fact]
    public void EnsureHealthy_throws_when_not_healthy()
    {
        var c = NewConfig(); // default Untested
        Assert.Throws<ConnectionUnhealthyException>(() => c.EnsureHealthy());
    }

    [Fact]
    public void EnsureHealthy_passes_when_healthy()
    {
        var c = NewConfig();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 10));
        c.EnsureHealthy(); // does not throw
    }
}
