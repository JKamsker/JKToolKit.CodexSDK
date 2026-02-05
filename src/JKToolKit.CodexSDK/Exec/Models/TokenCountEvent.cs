namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a token count event containing usage statistics.
/// </summary>
/// <remarks>
/// This event provides information about token usage for the current interaction,
/// including input, output, and reasoning tokens when available.
/// </remarks>
public record TokenCountEvent : CodexEvent
{
    /// <summary>
    /// Gets the number of input tokens used.
    /// </summary>
    /// <remarks>
    /// May be null if input token information is not available.
    /// </remarks>
    public int? InputTokens { get; init; }

    /// <summary>
    /// Gets the number of output tokens generated.
    /// </summary>
    /// <remarks>
    /// May be null if output token information is not available.
    /// </remarks>
    public int? OutputTokens { get; init; }

    /// <summary>
    /// Gets the number of reasoning tokens used.
    /// </summary>
    /// <remarks>
    /// May be null if reasoning token information is not available or if reasoning mode is disabled.
    /// </remarks>
    public int? ReasoningTokens { get; init; }

    /// <summary>
    /// Gets the rate limit usage information reported with this event, when available.
    /// </summary>
    public RateLimits? RateLimits { get; init; }

    /// <summary>
    /// Gets token usage for the last request/turn when provided by Codex.
    /// </summary>
    public TokenUsage? LastTokenUsage { get; init; }

    /// <summary>
    /// Gets token usage accumulated across the session when provided by Codex.
    /// </summary>
    public TokenUsage? TotalTokenUsage { get; init; }

    /// <summary>
    /// Gets the model context window size when provided by Codex.
    /// </summary>
    public int? ModelContextWindow { get; init; }
}

public sealed record TokenUsage(
    int? InputTokens,
    int? CachedInputTokens,
    int? OutputTokens,
    int? ReasoningOutputTokens,
    int? TotalTokens);
