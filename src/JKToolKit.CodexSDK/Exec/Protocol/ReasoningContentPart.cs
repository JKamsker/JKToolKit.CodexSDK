using System.Text.Json;

namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Base type for structured reasoning content parts.
/// </summary>
public abstract record ReasoningContentPart
{
    /// <summary>
    /// Gets the reasoning content discriminator.
    /// </summary>
    public required string ContentType { get; init; }
}

/// <summary>
/// Represents a text-bearing reasoning content part.
/// </summary>
public sealed record ReasoningTextContentPart : ReasoningContentPart
{
    /// <summary>
    /// Gets the text content.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// Forward-compat fallback for unknown reasoning content parts.
/// </summary>
public sealed record UnknownReasoningContentPart : ReasoningContentPart
{
    /// <summary>
    /// Gets the raw JSON content for unknown parts.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
