using Xunit;

namespace NexConvo.BuildingBlocks.Domain.Tests.Unit;

// NOTE: fully global-qualified type references are required here, not a style choice.
// `NexConvo.BuildingBlocks.Domain.ConnectionHealth` is both a namespace AND a type name
// (the ConnectionHealth record lives in the ConnectionHealth namespace). Because the parent
// namespace `NexConvo.BuildingBlocks.Domain` also declares sibling types (BaseEntity, etc.),
// Roslyn cannot bind a plain `using NexConvo.BuildingBlocks.Domain.ConnectionHealth;` or any
// non-rooted reference to the nested namespace from this external assembly (CS0234). This is
// a known namespace/type name collision, not a bug in the production code. See task-1-report.md.
public class ConnectionHealthTests
{
    [Fact]
    public void Healthy_factory_sets_success_and_status()
    {
        var h = global::NexConvo.BuildingBlocks.Domain.ConnectionHealth.ConnectionHealth.Healthy("Bucket reachable", 42);
        Assert.True(h.Success);
        Assert.Equal(global::NexConvo.BuildingBlocks.Domain.ConnectionHealth.ConnectionStatus.Healthy, h.Status);
        Assert.Equal("Bucket reachable", h.Detail);
        Assert.Null(h.ErrorMessage);
        Assert.Equal(42, h.LatencyMs);
    }

    [Fact]
    public void Failed_factory_sets_failure_and_error()
    {
        var h = global::NexConvo.BuildingBlocks.Domain.ConnectionHealth.ConnectionHealth.Failed("Access denied", 100);
        Assert.False(h.Success);
        Assert.Equal(global::NexConvo.BuildingBlocks.Domain.ConnectionHealth.ConnectionStatus.Failed, h.Status);
        Assert.Equal("Access denied", h.ErrorMessage);
        Assert.Null(h.Detail);
    }
}
