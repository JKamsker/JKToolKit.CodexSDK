namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents the start of applying a patch via <c>apply_patch</c>.
/// </summary>
public sealed record PatchApplyBeginEvent : CodexEvent
{
    public required string CallId { get; init; }
    public bool? AutoApproved { get; init; }
    public required IReadOnlyDictionary<string, PatchApplyFileChange> Changes { get; init; }
}

public sealed record PatchApplyFileChange
{
    public PatchApplyAddOperation? Add { get; init; }
    public PatchApplyUpdateOperation? Update { get; init; }
    public PatchApplyDeleteOperation? Delete { get; init; }
}

public sealed record PatchApplyAddOperation(string Content);

public sealed record PatchApplyDeleteOperation;

public sealed record PatchApplyUpdateOperation(
    string? UnifiedDiff,
    string? MovePath,
    string? OriginalContent,
    string? NewContent);

