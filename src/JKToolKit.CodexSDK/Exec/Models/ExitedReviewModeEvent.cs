namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents the end of a Codex <c>review</c> run, including structured review output.
/// </summary>
public sealed record ExitedReviewModeEvent : CodexEvent
{
    /// <summary>
    /// Gets the structured review output emitted by Codex.
    /// </summary>
    public required ReviewOutput ReviewOutput { get; init; }
}

