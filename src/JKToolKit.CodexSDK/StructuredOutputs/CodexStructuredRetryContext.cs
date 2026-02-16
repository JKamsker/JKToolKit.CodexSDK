namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Context for building a structured output retry prompt.
/// </summary>
public sealed record CodexStructuredRetryContext
{
    /// <summary>
    /// Gets the 1-based attempt number that failed.
    /// </summary>
    public required int Attempt { get; init; }

    /// <summary>
    /// Gets the configured maximum attempts.
    /// </summary>
    public required int MaxAttempts { get; init; }

    /// <summary>
    /// Gets the raw text captured from Codex for the failed attempt.
    /// </summary>
    public required string RawText { get; init; }

    /// <summary>
    /// Gets the extracted JSON, when extraction succeeded but deserialization failed.
    /// </summary>
    public string? ExtractedJson { get; init; }

    /// <summary>
    /// Gets the exception that caused the attempt to fail.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// Gets the exec session id, when applicable.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the exec log path, when applicable.
    /// </summary>
    public string? LogPath { get; init; }

    /// <summary>
    /// Gets the app-server thread id, when applicable.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets the app-server turn id, when applicable.
    /// </summary>
    public string? TurnId { get; init; }
}

