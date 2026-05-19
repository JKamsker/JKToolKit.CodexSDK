using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for a thread or turn execution environment.
/// </summary>
public sealed record class TurnEnvironmentParams
{
    /// <summary>
    /// Gets the upstream environment identifier.
    /// </summary>
    [JsonPropertyName("environmentId")]
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Gets the absolute working directory for this environment.
    /// </summary>
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }
}
