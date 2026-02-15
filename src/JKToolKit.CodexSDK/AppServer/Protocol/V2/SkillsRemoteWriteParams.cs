using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>skills/remote/write</c> request (v2 protocol).
/// </summary>
public sealed record class SkillsRemoteWriteParams
{
    /// <summary>
    /// Gets the hazelnut identifier for the remote skill.
    /// </summary>
    [JsonPropertyName("hazelnutId")]
    public required string HazelnutId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the skill should be preloaded.
    /// </summary>
    [JsonPropertyName("isPreload")]
    public bool IsPreload { get; init; }
}

