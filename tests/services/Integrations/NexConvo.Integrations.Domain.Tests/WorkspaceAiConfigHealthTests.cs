using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Enums;
using NexConvo.Integrations.Domain.Entities;
using Xunit;

namespace NexConvo.Integrations.Domain.Tests;

public class WorkspaceAiConfigHealthTests
{
    private static WorkspaceAiConfig NewConfig() =>
        new(Guid.NewGuid(), AiProviderType.OpenAI, "enc-key", null, "gpt-4o", null, null, true);

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
