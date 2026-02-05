namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents completion of applying a patch via <c>apply_patch</c>.
/// </summary>
public sealed record PatchApplyEndEvent : CodexEvent
{
    public required string CallId { get; init; }
    public string? Stdout { get; init; }
    public string? Stderr { get; init; }
    public bool? Success { get; init; }
}

