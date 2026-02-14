using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>skills/list</c> request (v2 protocol).
/// </summary>
public sealed record class SkillsListParams
{
    /// <summary>
    /// Gets an optional working directory scope, if supported upstream.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets optional extra roots for resolving skills relative to <see cref="Cwd"/>, if supported upstream.
    /// </summary>
    [JsonPropertyName("extraRootsForCwd")]
    public IReadOnlyList<string>? ExtraRootsForCwd { get; init; }
}

