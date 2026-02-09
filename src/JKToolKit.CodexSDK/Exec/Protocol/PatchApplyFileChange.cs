namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents file-level patch operations that Codex intends to apply.
/// </summary>
public sealed record PatchApplyFileChange
{
    /// <summary>
    /// Gets the add operation, if the file is being added.
    /// </summary>
    public PatchApplyAddOperation? Add { get; init; }

    /// <summary>
    /// Gets the update operation, if the file is being updated.
    /// </summary>
    public PatchApplyUpdateOperation? Update { get; init; }

    /// <summary>
    /// Gets the delete operation, if the file is being deleted.
    /// </summary>
    public PatchApplyDeleteOperation? Delete { get; init; }
}

/// <summary>
/// Represents a file add operation.
/// </summary>
/// <param name="Content">The file contents for the added file.</param>
public sealed record PatchApplyAddOperation(string Content);

/// <summary>
/// Represents a file delete operation.
/// </summary>
public sealed record PatchApplyDeleteOperation;

/// <summary>
/// Represents a file update operation.
/// </summary>
/// <param name="UnifiedDiff">Optional unified diff to apply.</param>
/// <param name="MovePath">Optional target path when the file is being moved/renamed.</param>
/// <param name="OriginalContent">Optional original content, if provided by Codex.</param>
/// <param name="NewContent">Optional new content, if provided by Codex.</param>
public sealed record PatchApplyUpdateOperation(
    string? UnifiedDiff,
    string? MovePath,
    string? OriginalContent,
    string? NewContent);
