using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>review/start</c> request (v2 protocol).
/// </summary>
public sealed record class ReviewStartParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the review target (wire shape).
    /// </summary>
    [JsonPropertyName("target")]
    public required JsonElement Target { get; init; }

    /// <summary>
    /// Gets an optional delivery mode (wire value).
    /// </summary>
    [JsonPropertyName("delivery")]
    public string? Delivery { get; init; }
}

