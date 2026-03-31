using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Result returned by <c>command/exec</c>.
/// </summary>
public sealed record class CommandExecResult
{
    /// <summary>
    /// Gets the process exit code.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets buffered stdout.
    /// </summary>
    public string Stdout { get; init; } = string.Empty;

    /// <summary>
    /// Gets buffered stderr.
    /// </summary>
    public string Stderr { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>command/exec/write</c>.
/// </summary>
public sealed class CommandExecWriteOptions
{
    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    public required string ProcessId { get; set; }

    /// <summary>
    /// Gets or sets optional base64-encoded stdin bytes to write.
    /// </summary>
    public string? DeltaBase64 { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stdin should be closed after writing.
    /// </summary>
    public bool? CloseStdin { get; set; }
}

/// <summary>
/// Result returned by <c>command/exec/write</c>.
/// </summary>
public sealed record class CommandExecWriteResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>command/exec/resize</c>.
/// </summary>
public sealed class CommandExecResizeOptions
{
    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    public required string ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the PTY size.
    /// </summary>
    public required CommandExecTerminalSize Size { get; set; }
}

/// <summary>
/// Result returned by <c>command/exec/resize</c>.
/// </summary>
public sealed record class CommandExecResizeResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>command/exec/terminate</c>.
/// </summary>
public sealed class CommandExecTerminateOptions
{
    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    public required string ProcessId { get; set; }
}

/// <summary>
/// Result returned by <c>command/exec/terminate</c>.
/// </summary>
public sealed record class CommandExecTerminateResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
