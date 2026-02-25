namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents a raw chain-of-thought reasoning event emitted by Codex (when enabled upstream).
/// </summary>
public sealed record AgentReasoningRawContentEvent : CodexEvent
{
    /// <summary>
    /// Gets the raw reasoning text.
    /// </summary>
    /// <remarks>
    /// This may contain sensitive information. Avoid persisting or logging this value by default.
    /// </remarks>
    public required string Text { get; init; }
}
