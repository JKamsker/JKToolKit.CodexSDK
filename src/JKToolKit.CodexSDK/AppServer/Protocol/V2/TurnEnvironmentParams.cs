using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for a thread or turn execution environment.
/// </summary>
public sealed record class TurnEnvironmentParams
{
    /// <summary>
    /// Gets the upstream environment identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.EnvironmentId)]
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Gets the absolute working directory for this environment.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public required string Cwd { get; init; }
}
