namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Thrown when the underlying <c>codex app-server</c> subprocess exits or the transport is otherwise disconnected.
/// </summary>
public sealed class CodexAppServerDisconnectedException : Exception
{
    /// <summary>
    /// The underlying Codex subprocess id, when available.
    /// </summary>
    public int? ProcessId { get; }

    /// <summary>
    /// The subprocess exit code, when available.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// A best-effort tail of stderr lines captured from the subprocess (may be empty).
    /// </summary>
    public IReadOnlyList<string> StderrTail { get; }

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CodexAppServerDisconnectedException(
        string message,
        int? processId,
        int? exitCode,
        IReadOnlyList<string>? stderrTail,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProcessId = processId;
        ExitCode = exitCode;
        StderrTail = stderrTail ?? Array.Empty<string>();
    }
}
