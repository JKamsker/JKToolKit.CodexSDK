using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of writing skills configuration via the app-server.
/// </summary>
public sealed record class SkillsConfigWriteResult
{
    /// <summary>
    /// Gets the effective enabled value after applying the config update.
    /// </summary>
    public bool? EffectiveEnabled { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

