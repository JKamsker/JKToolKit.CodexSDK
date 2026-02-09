namespace JKToolKit.CodexSDK.Exec;

/// <summary>
/// Describes why a Codex session ended.
/// </summary>
public enum SessionExitReason
{
    /// <summary>
    /// The exit reason is not yet known or could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The Codex process exited on its own (normal completion or crash not triggered by the SDK).
    /// </summary>
    Success = 1,

    /// <summary>
    /// The session was terminated because an idle timeout elapsed.
    /// </summary>
    Timeout = 2,

    /// <summary>
    /// The session was ended explicitly by the caller (for example by invoking ExitAsync on the session handle).
    /// </summary>
    Custom = 3
}
