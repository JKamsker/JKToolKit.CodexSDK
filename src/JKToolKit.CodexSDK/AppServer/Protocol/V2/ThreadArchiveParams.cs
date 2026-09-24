using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/archive</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadArchiveParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }
}
