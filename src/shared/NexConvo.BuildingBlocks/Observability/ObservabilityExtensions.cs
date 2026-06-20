using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace NexConvo.BuildingBlocks.Observability;

/// <summary>
/// Serilog + OpenTelemetry wiring shared by every service (skill Standard 9).
/// Structured JSON logs with trace correlation to Seq; traces + metrics to the OTLP collector.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Configures Serilog: structured JSON, trace-id enriched, Console + Seq sinks.</summary>
    public static IHostBuilder UseNexConvoSerilog(this IHostBuilder host, string serviceName) =>
        host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.With(new TraceCorrelationEnricher())
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .WriteTo.Seq(context.Configuration["Seq:Url"] ?? "http://localhost:5341"));

    /// <summary>Configures OpenTelemetry tracing + metrics with an optional OTLP exporter.</summary>
    public static IServiceCollection AddNexConvoOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var otlpEndpoint = configuration["Otel:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
