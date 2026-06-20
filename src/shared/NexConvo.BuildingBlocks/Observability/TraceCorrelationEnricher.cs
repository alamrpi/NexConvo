using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace NexConvo.BuildingBlocks.Observability;

/// <summary>
/// Adds the current OpenTelemetry trace id to every log event as <c>CorrelationId</c>
/// (plus <c>SpanId</c>), so a single id ties logs to traces across services
/// (skill Standard 9). No external enricher package needed.
/// </summary>
public sealed class TraceCorrelationEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("CorrelationId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
