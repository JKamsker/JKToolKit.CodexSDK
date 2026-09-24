using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>turn/steer</c> request (v2 protocol).
/// </summary>
public sealed record class TurnSteerParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets an optional client-provided id for the user message item created by this steer request.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ClientUserMessageId)]
    public string? ClientUserMessageId { get; init; }

    /// <summary>
    /// Gets the expected turn identifier (precondition).
    /// </summary>
    [JsonPropertyName("expectedTurnId")]
    public required string ExpectedTurnId { get; init; }

    /// <summary>
    /// Gets the input items (wire payloads).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Input)]
    public required IReadOnlyList<object> Input { get; init; }

    /// <summary>
    /// Gets optional turn-scoped Responses API client metadata.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ResponsesapiClientMetadata)]
    public IReadOnlyDictionary<string, string>? ResponsesApiClientMetadata { get; init; }

    /// <summary>
    /// Gets optional client-provided context fragments keyed by opaque source identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.AdditionalContext)]
    public IReadOnlyDictionary<string, TurnAdditionalContextEntryParams>? AdditionalContext { get; init; }
}

