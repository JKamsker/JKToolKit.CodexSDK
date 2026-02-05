namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a compaction record that replaces earlier history entries.
/// </summary>
public sealed record CompactedEvent : CodexEvent
{
    public required string Message { get; init; }
    public required IReadOnlyList<ResponseItemPayload> ReplacementHistory { get; init; }
}

