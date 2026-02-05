namespace JKToolKit.CodexSDK.Exec.Notifications;

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
}

