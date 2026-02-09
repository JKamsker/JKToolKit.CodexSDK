using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.Initialize;

/// <summary>
/// Wire response payload for the <c>initialize</c> request.
/// </summary>
public sealed record class InitializeResponse
{
    /// <summary>
    /// Gets the server user agent string.
    /// </summary>
    [JsonPropertyName("userAgent")]
    public required string UserAgent { get; init; }
}
