namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents completion of applying a patch via <c>apply_patch</c>.
/// </summary>
public sealed record PatchApplyEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this patch application.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the captured stdout from the patch application, when provided.
    /// </summary>
    public string? Stdout { get; init; }

    /// <summary>
    /// Gets the captured stderr from the patch application, when provided.
    /// </summary>
    public string? Stderr { get; init; }

    /// <summary>
    /// Gets whether the patch application was reported as successful, when provided.
    /// </summary>
    public bool? Success { get; init; }

    /// <summary>
    /// Gets the completion status for this patch application, when provided (e.g. completed, failed, declined).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets the per-file patch operations (add/update/delete), when provided.
    /// </summary>
    public IReadOnlyDictionary<string, PatchApplyFileChange>? Changes { get; init; }
}

