using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of listing skills via the app-server.
/// </summary>
public sealed record class SkillsListResult
{
    /// <summary>
    /// Gets the returned skills.
    /// </summary>
    public required IReadOnlyList<SkillDescriptor> Skills { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

