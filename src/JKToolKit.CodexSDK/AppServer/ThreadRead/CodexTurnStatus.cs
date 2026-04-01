namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Enumerates the possible <c>turn/read</c> statuses surfaced by the upstream API.
/// </summary>
public enum CodexTurnStatus
{
    /// <summary>
    /// The status is not recognized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The turn completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The turn was interrupted.
    /// </summary>
    Interrupted,

    /// <summary>
    /// The turn failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The turn is still in progress.
    /// </summary>
    InProgress
}

internal static class CodexTurnStatusExtensions
{
    public static CodexTurnStatus Parse(string? value) => value switch
    {
        "completed" => CodexTurnStatus.Completed,
        "interrupted" => CodexTurnStatus.Interrupted,
        "failed" => CodexTurnStatus.Failed,
        "inProgress" or "in_progress" => CodexTurnStatus.InProgress,
        _ => CodexTurnStatus.Unknown
    };
}
