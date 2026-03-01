using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>item/tool/call</c> server request (v2 protocol).
/// </summary>
public sealed record class DynamicToolCallParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    [JsonPropertyName("turnId")]
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the call id.
    /// </summary>
    [JsonPropertyName("callId")]
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    /// <summary>
    /// Gets the tool arguments payload.
    /// </summary>
    [JsonPropertyName("arguments")]
    public required JsonElement Arguments { get; init; }
}
