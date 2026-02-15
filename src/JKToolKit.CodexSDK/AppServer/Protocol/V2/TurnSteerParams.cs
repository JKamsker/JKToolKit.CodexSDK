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
    /// Gets the expected turn identifier (precondition).
    /// </summary>
    [JsonPropertyName("expectedTurnId")]
    public required string ExpectedTurnId { get; init; }

    /// <summary>
    /// Gets the input items (wire payloads).
    /// </summary>
    [JsonPropertyName("input")]
    public required IReadOnlyList<object> Input { get; init; }
}

