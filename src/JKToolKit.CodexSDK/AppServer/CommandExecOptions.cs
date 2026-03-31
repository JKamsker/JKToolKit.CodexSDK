using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>command/exec</c>.
/// </summary>
public sealed class CommandExecOptions
{
    /// <summary>
    /// Gets or sets the command argv vector.
    /// </summary>
    public IReadOnlyList<string> Command { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets an optional working directory.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stdout/stderr capture truncation should be disabled.
    /// </summary>
    public bool? DisableOutputCap { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the timeout should be disabled.
    /// </summary>
    public bool? DisableTimeout { get; set; }

    /// <summary>
    /// Gets or sets optional environment overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Env { get; set; }

    /// <summary>
    /// Gets or sets the optional per-stream output cap in bytes.
    /// </summary>
    public int? OutputBytesCap { get; set; }

    /// <summary>
    /// Gets or sets the optional client-supplied process identifier.
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the optional sandbox policy override.
    /// </summary>
    public SandboxPolicy? SandboxPolicy { get; set; }

    /// <summary>
    /// Gets or sets the optional initial PTY size.
    /// </summary>
    public CommandExecTerminalSize? Size { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether follow-up stdin writes are allowed.
    /// </summary>
    public bool? StreamStdin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stdout/stderr should be streamed via notifications.
    /// </summary>
    public bool? StreamStdoutStderr { get; set; }

    /// <summary>
    /// Gets or sets the optional timeout in milliseconds.
    /// </summary>
    public long? TimeoutMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether PTY mode should be enabled.
    /// </summary>
    public bool? Tty { get; set; }
}

/// <summary>
/// Terminal size for PTY-backed <c>command/exec</c> sessions.
/// </summary>
public sealed record class CommandExecTerminalSize
{
    /// <summary>
    /// Gets or sets the terminal width in character cells.
    /// </summary>
    public required ushort Columns { get; init; }

    /// <summary>
    /// Gets or sets the terminal height in character cells.
    /// </summary>
    public required ushort Rows { get; init; }
}
