using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>app/list</c> request (v2 protocol).
/// </summary>
public sealed record class AppListParams
{
    /// <summary>
    /// Gets an optional working directory scope, if supported upstream.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }
}

