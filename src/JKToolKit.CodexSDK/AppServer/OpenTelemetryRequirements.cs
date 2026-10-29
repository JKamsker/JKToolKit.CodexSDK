using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents managed OpenTelemetry requirements returned by <c>configRequirements/read</c>.
/// </summary>
public sealed record class OpenTelemetryRequirements
{
    /// <summary>Gets the maximum number of tool-result bytes that may be logged.</summary>
    public long? ToolResultMaxBytes { get; init; }

    /// <summary>Gets whether user prompts may be logged.</summary>
    public bool? LogUserPrompt { get; init; }

    /// <summary>Gets whether agent responses may be logged.</summary>
    public bool? LogAgentResponses { get; init; }

    /// <summary>Gets whether Guardian assessments may be logged.</summary>
    public bool? LogGuardianAssessments { get; init; }

    /// <summary>Gets the managed telemetry environment name.</summary>
    public string? Environment { get; init; }

    /// <summary>Gets the default exporter union as raw JSON.</summary>
    public JsonElement? Exporter { get; init; }

    /// <summary>Gets the trace exporter union as raw JSON.</summary>
    public JsonElement? TraceExporter { get; init; }

    /// <summary>Gets the metrics exporter union as raw JSON.</summary>
    public JsonElement? MetricsExporter { get; init; }

    /// <summary>Gets managed span attributes.</summary>
    public IReadOnlyDictionary<string, string>? SpanAttributes { get; init; }

    /// <summary>Gets managed W3C tracestate entries.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? TraceState { get; init; }

    /// <summary>Gets the raw JSON requirements payload.</summary>
    public required JsonElement Raw { get; init; }
}
