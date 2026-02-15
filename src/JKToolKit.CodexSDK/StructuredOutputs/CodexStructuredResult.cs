namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Represents a structured output result parsed into a DTO.
/// </summary>
public sealed record CodexStructuredResult<T>
{
    /// <summary>
    /// Gets the parsed DTO value.
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Gets the extracted JSON text that was deserialized into <see cref="Value"/>.
    /// </summary>
    public required string RawJson { get; init; }

    /// <summary>
    /// Gets the original raw text captured from Codex before JSON extraction.
    /// </summary>
    public required string RawText { get; init; }

    /// <summary>
    /// Gets the session identifier when produced by exec-mode.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the session log path when produced by exec-mode.
    /// </summary>
    public string? LogPath { get; init; }

    /// <summary>
    /// Gets the thread identifier when produced by app-server mode.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier when produced by app-server mode.
    /// </summary>
    public string? TurnId { get; init; }
}

