namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Indicates that earlier conversation context was compacted.
/// </summary>
public sealed record CompactionCheckpointWarningEvent : CodexEvent
{
    public required string Message { get; init; }
}

