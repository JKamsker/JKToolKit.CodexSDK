using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>turn/interrupt</c> request (v2 protocol).
/// </summary>
public sealed record class TurnInterruptParams
{
    /// <summary>
    /// Gets the thread identifier containing the turn.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier to interrupt.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.TurnId)]
    public required string TurnId { get; init; }
}
