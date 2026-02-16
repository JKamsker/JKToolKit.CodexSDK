using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents config metadata for a single origin entry returned by <c>config/read</c>.
/// </summary>
public sealed record class ConfigLayerMetadataInfo
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
    /// Gets the raw JSON payload for forward compatibility.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

