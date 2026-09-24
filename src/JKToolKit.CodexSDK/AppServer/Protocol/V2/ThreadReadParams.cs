using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/read</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadReadParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets a value indicating whether turns should be included in the raw response.
    /// </summary>
    [JsonPropertyName("includeTurns")]
    public bool? IncludeTurns { get; init; }
}
