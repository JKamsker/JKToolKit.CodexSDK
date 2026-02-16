using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Optional callbacks for observing progress while running a structured exec session.
/// </summary>
public sealed class CodexStructuredRunProgress
{
    /// <summary>
    /// Called when an attempt is starting (attempt 1 starts a new session, later attempts resume).
    /// </summary>
    public Action<int, int, CodexStructuredAttemptKind>? AttemptStarting { get; init; }

    /// <summary>
    /// Called once the session is started/resumed and the SDK has the session id and log path.
    /// </summary>
    public Action<SessionId, string?>? SessionLocated { get; init; }

    /// <summary>
    /// Called for each event observed while awaiting the structured result.
    /// </summary>
    public Action<CodexEvent>? EventReceived { get; init; }

    /// <summary>
    /// Called when parsing failed and the operation will retry (if attempts remain).
    /// </summary>
    public Action<int, Exception>? ParseFailed { get; init; }
}

/// <summary>
/// Indicates whether a structured attempt is starting a new session or resuming an existing one.
/// </summary>
public enum CodexStructuredAttemptKind
{
    /// <summary>
    /// Start a new <c>codex exec</c> session.
    /// </summary>
    Start = 0,

    /// <summary>
    /// Resume an existing session via <c>codex exec resume</c>.
    /// </summary>
    Resume = 1
}
