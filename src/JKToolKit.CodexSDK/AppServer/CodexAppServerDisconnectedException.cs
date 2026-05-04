namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Thrown when the underlying <c>codex app-server</c> process exits or the transport is otherwise disconnected.
/// </summary>
public sealed class CodexAppServerDisconnectedException : Exception
{
    /// <summary>
    /// The underlying Codex process id, when available.
    /// </summary>
    public int? ProcessId { get; }

    /// <summary>
    /// The process exit code, when available.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// A best-effort diagnostic tail captured from the transport or process (may be empty).
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
