namespace NexConvo.BuildingBlocks.Domain.ConnectionHealth;

public enum ConnectionStatus
{
    Untested = 0,
    Healthy = 1,
    Degraded = 2,
    Failed = 3,
}
