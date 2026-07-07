namespace NexConvo.BuildingBlocks.Domain.ConnectionHealth;

public sealed record ConnectionHealth(
    bool Success,
    ConnectionStatus Status,
    string? Detail,
    string? ErrorMessage,
    int? LatencyMs)
{
    public static ConnectionHealth Healthy(string? detail, int? latencyMs) =>
        new(true, ConnectionStatus.Healthy, detail, null, latencyMs);

    public static ConnectionHealth Failed(string? error, int? latencyMs) =>
        new(false, ConnectionStatus.Failed, null, error, latencyMs);
}
