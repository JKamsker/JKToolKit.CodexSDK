namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Thrown when structured output parsing fails after exhausting automatic retries.
/// </summary>
public sealed class CodexStructuredOutputRetryFailedException : Exception
{
    /// <summary>
    /// Gets the number of attempts that were made.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Gets the exec session id, when applicable.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Gets the exec log path, when applicable.
    /// </summary>
    public string? LogPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexStructuredOutputRetryFailedException"/> class.
    /// </summary>
    public CodexStructuredOutputRetryFailedException(
        int attempts,
        string message,
        Exception innerException,
        string? sessionId,
        string? logPath)
        : base(message, innerException)
    {
        Attempts = attempts;
        SessionId = sessionId;
        LogPath = logPath;
    }
}

