using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
