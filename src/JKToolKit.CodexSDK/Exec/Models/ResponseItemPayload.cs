using System.Text.Json;

namespace JKToolKit.CodexSDK.Models;

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

public sealed record ReasoningResponseItemPayload : ResponseItemPayload
{
    public required IReadOnlyList<string> SummaryTexts { get; init; }
    public string? EncryptedContent { get; init; }
}

public sealed record MessageResponseItemPayload : ResponseItemPayload
{
    public string? Role { get; init; }
    public required IReadOnlyList<ResponseMessageContentPart> Content { get; init; }

    public IReadOnlyList<string> TextParts =>
        Content.OfType<ResponseMessageTextContentPart>()
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
}

public sealed record FunctionCallResponseItemPayload : ResponseItemPayload
{
    public string? Name { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? CallId { get; init; }
}

public sealed record FunctionCallOutputResponseItemPayload : ResponseItemPayload
{
    public string? CallId { get; init; }
    public string? Output { get; init; }
}

public sealed record CustomToolCallResponseItemPayload : ResponseItemPayload
{
    public string? Status { get; init; }
    public string? CallId { get; init; }
    public string? Name { get; init; }
    public string? Input { get; init; }
}

public sealed record CustomToolCallOutputResponseItemPayload : ResponseItemPayload
{
    public string? CallId { get; init; }
    public string? Output { get; init; }
}

public sealed record WebSearchCallResponseItemPayload : ResponseItemPayload
{
    public string? Status { get; init; }
    public WebSearchAction? Action { get; init; }
}

public sealed record WebSearchAction(string? Type, string? Query, IReadOnlyList<string>? Queries);

public sealed record GhostSnapshotResponseItemPayload : ResponseItemPayload
{
    public GhostCommit? GhostCommit { get; init; }
}

public sealed record CompactionResponseItemPayload : ResponseItemPayload
{
    public string? EncryptedContent { get; init; }
}

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
    public required JsonElement Raw { get; init; }
}

/// <summary>Base type for content parts in a message payload.</summary>
public abstract record ResponseMessageContentPart
{
    public required string ContentType { get; init; }
}

public abstract record ResponseMessageTextContentPart : ResponseMessageContentPart
{
    public required string Text { get; init; }
}

public sealed record ResponseMessageInputTextPart : ResponseMessageTextContentPart;

public sealed record ResponseMessageOutputTextPart : ResponseMessageTextContentPart;

public sealed record ResponseMessageInputImagePart : ResponseMessageContentPart
{
    public required string ImageUrl { get; init; }
}

public sealed record UnknownResponseMessageContentPart : ResponseMessageContentPart
{
    public required JsonElement Raw { get; init; }
}
