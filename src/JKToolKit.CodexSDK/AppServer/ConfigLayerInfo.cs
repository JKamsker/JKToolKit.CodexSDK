using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a config layer returned by <c>config/read</c> when <c>includeLayers</c> is enabled.
/// </summary>
public sealed record class ConfigLayerInfo
{
    /// <summary>
    /// Gets the layer source.
    /// </summary>
    public required ConfigLayerSourceInfo Name { get; init; }

    /// <summary>
    /// Gets a version string for the config layer.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the layer config payload (raw JSON).
    /// </summary>
    public required JsonElement Config { get; init; }

    /// <summary>
    /// Gets an optional disabled reason string when the layer was disabled.
    /// </summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for forward compatibility.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

