using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NexConvo.BuildingBlocks.Tests.Ai;

/// <summary>
/// Records every formatted log message so tests can assert what was — and was NOT — logged
/// (e.g. that a secret never leaks into a log line).
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public IEnumerable<string> All => Messages;

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Enqueue(formatter(state, exception));
    }
}
