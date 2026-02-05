namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents token usage statistics reported by Codex.
/// </summary>
/// <param name="InputTokens">Number of input tokens used.</param>
/// <param name="CachedInputTokens">Number of cached input tokens used, when provided.</param>
/// <param name="OutputTokens">Number of output tokens generated.</param>
/// <param name="ReasoningOutputTokens">Number of reasoning tokens generated, when provided.</param>
/// <param name="TotalTokens">Total tokens used, when provided.</param>
public sealed record TokenUsage(
    int? InputTokens,
    int? CachedInputTokens,
    int? OutputTokens,
    int? ReasoningOutputTokens,
    int? TotalTokens);
