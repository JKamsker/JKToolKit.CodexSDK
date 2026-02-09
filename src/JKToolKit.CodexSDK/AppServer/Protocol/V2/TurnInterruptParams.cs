using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>turn/interrupt</c> request (v2 protocol).
/// </summary>
public sealed record class TurnInterruptParams
{
    /// <summary>
    /// Gets the thread identifier containing the turn.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier to interrupt.
    /// </summary>
    [JsonPropertyName("turnId")]
    public required string TurnId { get; init; }
}
