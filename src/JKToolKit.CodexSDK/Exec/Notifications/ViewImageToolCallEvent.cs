namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents a notification that a local image was attached via the view_image tool.
/// </summary>
public sealed record ViewImageToolCallEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this view_image invocation.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the local filesystem path that was provided to the tool.
    /// </summary>
    public required string Path { get; init; }
}
