using System.Text.Json;

namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Base type for all <c>response_item</c> payloads emitted by Codex.
/// </summary>
public abstract record ResponseItemPayload
{
    /// <summary>
    /// Gets the payload discriminator (e.g. <c>message</c>, <c>reasoning</c>, <c>function_call</c>).
    /// </summary>
    public required string PayloadType { get; init; }
}

/// <summary>
/// Represents a <c>reasoning</c> response item payload emitted by Codex.
/// </summary>
public sealed record ReasoningResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the reasoning summary texts, when provided.
    /// </summary>
    public required IReadOnlyList<string> SummaryTexts { get; init; }

    /// <summary>
    /// Gets the encrypted reasoning content, when provided.
    /// </summary>
    public string? EncryptedContent { get; init; }
}

/// <summary>
/// Represents a <c>message</c> response item payload emitted by Codex.
/// </summary>
public sealed record MessageResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the message role (e.g. <c>assistant</c>, <c>user</c>), when provided.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Gets the structured content parts for this message.
    /// </summary>
    public required IReadOnlyList<ResponseMessageContentPart> Content { get; init; }

    /// <summary>
    /// Gets extracted non-empty text parts from <see cref="Content"/>.
    /// </summary>
    public IReadOnlyList<string> TextParts =>
        Content.OfType<ResponseMessageTextContentPart>()
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
}

/// <summary>
/// Represents a <c>function_call</c> response item payload emitted by Codex.
/// </summary>
public sealed record FunctionCallResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the function name, when provided.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the function arguments JSON string, when provided.
    /// </summary>
    public string? ArgumentsJson { get; init; }

    /// <summary>
    /// Gets the call id associated with this function call, when provided.
    /// </summary>
    public string? CallId { get; init; }
}

/// <summary>
/// Represents a <c>function_call_output</c> response item payload emitted by Codex.
/// </summary>
public sealed record FunctionCallOutputResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the call id associated with this output, when provided.
    /// </summary>
    public string? CallId { get; init; }

    /// <summary>
    /// Gets the output content, when provided.
    /// </summary>
    public string? Output { get; init; }
}

/// <summary>
/// Represents a <c>custom_tool_call</c> response item payload emitted by Codex.
/// </summary>
public sealed record CustomToolCallResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the status of the tool call (e.g. in_progress, completed), when provided.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets the call id associated with this tool call, when provided.
    /// </summary>
    public string? CallId { get; init; }

    /// <summary>
    /// Gets the tool name, when provided.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the tool input payload, when provided.
    /// </summary>
    public string? Input { get; init; }
}

/// <summary>
/// Represents a <c>custom_tool_call_output</c> response item payload emitted by Codex.
/// </summary>
public sealed record CustomToolCallOutputResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the call id associated with this output, when provided.
    /// </summary>
    public string? CallId { get; init; }

    /// <summary>
    /// Gets the output payload, when provided.
    /// </summary>
    public string? Output { get; init; }
}

/// <summary>
/// Represents a <c>web_search_call</c> response item payload emitted by Codex.
/// </summary>
public sealed record WebSearchCallResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the status of the web search call, when provided.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets the web search action, when provided.
    /// </summary>
    public WebSearchAction? Action { get; init; }
}

/// <summary>
/// Represents a normalized web search action.
/// </summary>
/// <param name="Type">Action type (implementation-defined).</param>
/// <param name="Query">The query string, when provided.</param>
/// <param name="Queries">A list of queries, when provided.</param>
public sealed record WebSearchAction(string? Type, string? Query, IReadOnlyList<string>? Queries);

/// <summary>
/// Represents a <c>ghost_snapshot</c> response item payload emitted by Codex.
/// </summary>
public sealed record GhostSnapshotResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the ghost commit metadata, when provided.
    /// </summary>
    public GhostCommit? GhostCommit { get; init; }
}

/// <summary>
/// Represents a <c>compaction</c> response item payload emitted by Codex.
/// </summary>
public sealed record CompactionResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets encrypted compaction content, when provided.
    /// </summary>
    public string? EncryptedContent { get; init; }
}

/// <summary>
/// Represents metadata about a "ghost" commit.
/// </summary>
/// <param name="Id">Commit id, when provided.</param>
/// <param name="Parent">Parent commit id, when provided.</param>
/// <param name="PreexistingUntrackedFiles">Untracked files present before the commit, when provided.</param>
/// <param name="PreexistingUntrackedDirs">Untracked directories present before the commit, when provided.</param>
public sealed record GhostCommit(
    string? Id,
    string? Parent,
    IReadOnlyList<string>? PreexistingUntrackedFiles,
    IReadOnlyList<string>? PreexistingUntrackedDirs);

/// <summary>
/// Forward-compat fallback for unknown payload types.
/// Smoke tests should ensure this isn't used for known Codex CLI versions.
/// </summary>
public sealed record UnknownResponseItemPayload : ResponseItemPayload
{
    /// <summary>
    /// Gets the raw payload JSON for unknown payload types.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>Base type for content parts in a message payload.</summary>
public abstract record ResponseMessageContentPart
{
    /// <summary>
    /// Gets the content-part discriminator.
    /// </summary>
    public required string ContentType { get; init; }
}

/// <summary>
/// Base type for text-bearing content parts.
/// </summary>
public abstract record ResponseMessageTextContentPart : ResponseMessageContentPart
{
    /// <summary>
    /// Gets the text content.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// Represents an input text content part.
/// </summary>
public sealed record ResponseMessageInputTextPart : ResponseMessageTextContentPart;

/// <summary>
/// Represents an output text content part.
/// </summary>
public sealed record ResponseMessageOutputTextPart : ResponseMessageTextContentPart;

/// <summary>
/// Represents an input image content part.
/// </summary>
public sealed record ResponseMessageInputImagePart : ResponseMessageContentPart
{
    /// <summary>
    /// Gets the image url.
    /// </summary>
    public required string ImageUrl { get; init; }
}

/// <summary>
/// Forward-compat fallback for unknown message content parts.
/// </summary>
public sealed record UnknownResponseMessageContentPart : ResponseMessageContentPart
{
    /// <summary>
    /// Gets the raw JSON content for unknown parts.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
