namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents the start of a Codex <c>review</c> run.
/// </summary>
public sealed record EnteredReviewModeEvent : CodexEvent
{
    /// <summary>
    /// Gets the prompt passed to the review run, when provided.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// Gets a user-facing hint or guidance emitted by Codex, when provided.
    /// </summary>
    public string? UserFacingHint { get; init; }

    /// <summary>
    /// Gets the review target metadata, when provided.
    /// </summary>
    public ReviewTarget? Target { get; init; }
}
