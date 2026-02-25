namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of a shell command executed by the agent.
/// </summary>
public sealed record ExecCommandEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this command.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the underlying PTY/process id, when provided.
    /// </summary>
    public string? ProcessId { get; init; }

    /// <summary>
    /// Gets the turn id that this command belongs to, when provided.
    /// </summary>
    public string? TurnId { get; init; }

    /// <summary>
    /// Gets the executed command argv, when provided.
    /// </summary>
    public IReadOnlyList<string>? Command { get; init; }

    /// <summary>
    /// Gets the command's working directory, when provided.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the command source (agent/user_shell/etc.), when provided.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets raw interaction input for unified exec sessions, when provided.
    /// </summary>
    public string? InteractionInput { get; init; }

    /// <summary>
    /// Gets captured stdout, when provided.
    /// </summary>
    public string? Stdout { get; init; }

    /// <summary>
    /// Gets captured stderr, when provided.
    /// </summary>
    public string? Stderr { get; init; }

    /// <summary>
    /// Gets the aggregated output summary, when provided.
    /// </summary>
    public string? AggregatedOutput { get; init; }

    /// <summary>
    /// Gets the process exit code, when provided.
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// Gets the execution duration, when provided.
    /// </summary>
    public string? Duration { get; init; }

    /// <summary>
    /// Gets the formatted output as seen by the model, when provided.
    /// </summary>
    public string? FormattedOutput { get; init; }

    /// <summary>
    /// Gets the completion status (completed/failed/declined), when provided.
    /// </summary>
    public string? Status { get; init; }
}
