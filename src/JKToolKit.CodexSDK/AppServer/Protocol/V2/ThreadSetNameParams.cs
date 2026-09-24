using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/name/set</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadSetNameParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the new thread name.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Name)]
    public required string Name { get; init; }
}
