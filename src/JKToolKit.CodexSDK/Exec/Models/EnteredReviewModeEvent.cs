namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents the start of a Codex <c>review</c> run.
/// </summary>
public sealed record EnteredReviewModeEvent : CodexEvent
{
    public string? Prompt { get; init; }
    public string? UserFacingHint { get; init; }
    public ReviewTarget? Target { get; init; }
}

public sealed record ReviewTarget(
    string Type,
    string? Branch,
    string? Sha,
    string? Title,
    string? Instructions);
