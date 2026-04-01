using System.Text.Json;

namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Base type for structured function/custom-tool output content items.
/// </summary>
public abstract record FunctionToolOutputContentPart
{
    /// <summary>
    /// Gets the content-item discriminator.
    /// </summary>
    public required string ContentType { get; init; }
}

/// <summary>
/// Represents a text content item returned by a function/custom tool.
/// </summary>
public sealed record FunctionToolOutputInputTextPart : FunctionToolOutputContentPart
{
    /// <summary>
    /// Gets the text content.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// Represents an image content item returned by a function/custom tool.
/// </summary>
public sealed record FunctionToolOutputInputImagePart : FunctionToolOutputContentPart
{
    /// <summary>
    /// Gets the image url.
    /// </summary>
    public required string ImageUrl { get; init; }

    /// <summary>
    /// Gets the optional image detail hint, when provided.
    /// </summary>
    public string? Detail { get; init; }
}

/// <summary>
/// Forward-compat fallback for unknown function/custom-tool output content items.
/// </summary>
public sealed record UnknownFunctionToolOutputContentPart : FunctionToolOutputContentPart
{
    /// <summary>
    /// Gets the raw JSON content for unknown parts.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
