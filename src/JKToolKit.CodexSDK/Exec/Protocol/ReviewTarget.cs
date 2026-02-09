namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents the target being reviewed by Codex review mode.
/// </summary>
/// <param name="Type">Target type (e.g. branch, sha).</param>
/// <param name="Branch">Target branch name, when applicable.</param>
/// <param name="Sha">Target commit SHA, when applicable.</param>
/// <param name="Title">Optional title for the target, when applicable.</param>
/// <param name="Instructions">Optional instructions provided for the review target.</param>
public sealed record ReviewTarget(
    string Type,
    string? Branch,
    string? Sha,
    string? Title,
    string? Instructions);
