using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>config/read</c> request (v2 protocol).
/// </summary>
public sealed record class ConfigReadParams
{
    /// <summary>
    /// Gets a value indicating whether to include resolved config layers in the response.
    /// </summary>
    [JsonPropertyName("includeLayers")]
    public required bool IncludeLayers { get; init; }

    /// <summary>
    /// Gets an optional working directory used to resolve project config layers.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public string? Cwd { get; init; }
}

