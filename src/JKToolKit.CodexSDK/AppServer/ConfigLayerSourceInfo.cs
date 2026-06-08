using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a config layer source descriptor as returned by <c>config/read</c>.
/// </summary>
public sealed record class ConfigLayerSourceInfo
{
    /// <summary>
    /// Gets the layer source type (e.g. "user", "system", "project", "mdm", "sessionFlags").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the stable identifier for an enterprise-managed config layer, when present.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the display name for an enterprise-managed config layer, when present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the MDM domain when <see cref="Type"/> is "mdm".
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets the MDM key when <see cref="Type"/> is "mdm".
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Gets the absolute path to a config file when present (e.g. "user", "system").
    /// </summary>
    public string? File { get; init; }

    /// <summary>
    /// Gets the selected profile-v2 layer name when <see cref="Type"/> is "user" and the layer represents a profile.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>
    /// Gets the absolute path to the <c>.codex</c> folder when <see cref="Type"/> is "project".
    /// </summary>
    public string? DotCodexFolder { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for forward compatibility.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

