using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.Initialize;

/// <summary>
/// Wire response payload for the <c>initialize</c> request.
/// </summary>
public sealed record class InitializeResponse
{
    /// <summary>
    /// Gets the server user agent string.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.UserAgent)]
    public required string UserAgent { get; init; }
}
