namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a session metadata event containing session configuration and context.
/// </summary>
/// <remarks>
/// This event is typically emitted at the start of a session and contains
/// information about the session identifier and working directory.
/// </remarks>
public record SessionMetaEvent : CodexEvent
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public required SessionId SessionId { get; init; }

    /// <summary>
    /// Gets the current working directory for the session.
    /// </summary>
    /// <remarks>
    /// May be null if the working directory information is not available.
    /// </remarks>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the Codex CLI version when provided.
    /// </summary>
    public string? CliVersion { get; init; }

    /// <summary>
    /// Gets the originator identifier when provided (e.g. <c>codex_cli_rs</c>).
    /// </summary>
    public string? Originator { get; init; }

    /// <summary>
    /// Gets the emitted source when provided (e.g. <c>cli</c>).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the subagent name when this session was emitted by a subagent source (e.g. <c>review</c>).
    /// </summary>
    public string? SourceSubagent { get; init; }

    /// <summary>
    /// Gets the model provider when provided.
    /// </summary>
    public string? ModelProvider { get; init; }
}
