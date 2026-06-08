using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>turn/steer</c> request (v2 protocol).
/// </summary>
public sealed record class TurnSteerParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets an optional client-provided id for the user message item created by this steer request.
    /// </summary>
    [JsonPropertyName("clientUserMessageId")]
    public string? ClientUserMessageId { get; init; }

    /// <summary>
    /// Gets the expected turn identifier (precondition).
    /// </summary>
    [JsonPropertyName("expectedTurnId")]
    public required string ExpectedTurnId { get; init; }

    /// <summary>
    /// Gets the input items (wire payloads).
    /// </summary>
    [JsonPropertyName("input")]
    public required IReadOnlyList<object> Input { get; init; }

    /// <summary>
    /// Gets optional turn-scoped Responses API client metadata.
    /// </summary>
    [JsonPropertyName("responsesapiClientMetadata")]
    public IReadOnlyDictionary<string, string>? ResponsesApiClientMetadata { get; init; }

    /// <summary>
    /// Gets optional client-provided context fragments keyed by opaque source identifier.
    /// </summary>
    [JsonPropertyName("additionalContext")]
    public IReadOnlyDictionary<string, TurnAdditionalContextEntryParams>? AdditionalContext { get; init; }
}

