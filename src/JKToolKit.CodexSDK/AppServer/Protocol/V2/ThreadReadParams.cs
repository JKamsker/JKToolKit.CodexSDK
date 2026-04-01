using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/read</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadReadParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets a value indicating whether turns should be included in the raw response.
    /// </summary>
    [JsonPropertyName("includeTurns")]
    public bool? IncludeTurns { get; init; }
}
