using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/loaded/list</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadLoadedListParams
{
    /// <summary>
    /// Gets an optional cursor for paging.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cursor)]
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets an optional page size, if supported upstream.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Limit)]
    public int? Limit { get; init; }
}

