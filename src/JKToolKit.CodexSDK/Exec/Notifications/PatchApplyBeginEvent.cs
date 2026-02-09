namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents the start of applying a patch via <c>apply_patch</c>.
/// </summary>
public sealed record PatchApplyBeginEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this patch application.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets whether the patch application was auto-approved, when known.
    /// </summary>
    public bool? AutoApproved { get; init; }

    /// <summary>
    /// Gets the per-file patch operations (add/update/delete).
    /// </summary>
    public required IReadOnlyDictionary<string, PatchApplyFileChange> Changes { get; init; }
}

