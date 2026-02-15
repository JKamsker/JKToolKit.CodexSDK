using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of reading remote skills via the app-server.
/// </summary>
public sealed record class RemoteSkillsReadResult
{
    /// <summary>
    /// Gets the returned remote skills.
    /// </summary>
    public required IReadOnlyList<RemoteSkillDescriptor> Skills { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

