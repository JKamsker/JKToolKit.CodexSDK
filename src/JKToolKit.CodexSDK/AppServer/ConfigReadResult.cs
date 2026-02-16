using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of reading the effective configuration via the app-server.
/// </summary>
public sealed record class ConfigReadResult
{
    /// <summary>
    /// Gets the effective merged config object.
    /// </summary>
    public required JsonElement Config { get; init; }

    /// <summary>
    /// Gets the config origins map (key -> metadata) when present.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigLayerMetadataInfo>? Origins { get; init; }

    /// <summary>
    /// Gets the config layer stack when <see cref="ConfigReadOptions.IncludeLayers"/> was enabled.
    /// </summary>
    public IReadOnlyList<ConfigLayerInfo>? Layers { get; init; }

    /// <summary>
    /// Gets parsed MCP server entries from the effective config (<c>mcp_servers</c>), when present.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfigInfo>? McpServers { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

