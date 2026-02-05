namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents a compaction record that replaces earlier history entries.
/// </summary>
public sealed record CompactedEvent : CodexEvent
{
    /// <summary>
    /// Gets a human-readable summary message about the compaction.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the replacement history emitted after compaction.
    /// </summary>
    public required IReadOnlyList<ResponseItemPayload> ReplacementHistory { get; init; }
}

