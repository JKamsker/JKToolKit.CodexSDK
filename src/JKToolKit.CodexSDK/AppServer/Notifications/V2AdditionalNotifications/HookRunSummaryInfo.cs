using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Represents one hook output entry emitted within a hook run summary.
/// </summary>
public sealed record class HookOutputEntryInfo
{
    /// <summary>
    /// Gets the upstream entry kind wire value.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the entry text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the raw entry payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the typed hook run summary reported by hook notifications.
/// </summary>
public sealed record class HookRunSummaryInfo
{
    /// <summary>
    /// Gets the hook run identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the upstream event-name wire value.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the upstream handler-type wire value.
    /// </summary>
    public required string HandlerType { get; init; }

    /// <summary>
    /// Gets the upstream execution-mode wire value.
    /// </summary>
    public required string ExecutionMode { get; init; }

    /// <summary>
    /// Gets the upstream scope wire value.
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets the hook source path.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Gets the display-order value assigned by upstream.
    /// </summary>
    public long DisplayOrder { get; init; }

    /// <summary>
    /// Gets the upstream hook-status wire value.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets an optional status message.
    /// </summary>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// Gets the hook start timestamp.
    /// </summary>
    public long StartedAt { get; init; }

    /// <summary>
    /// Gets the hook completion timestamp when present.
    /// </summary>
    public long? CompletedAt { get; init; }

    /// <summary>
    /// Gets the hook duration in milliseconds when present.
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Gets the hook output entries.
    /// </summary>
    public required IReadOnlyList<HookOutputEntryInfo> Entries { get; init; }

    /// <summary>
    /// Gets the raw hook run payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
