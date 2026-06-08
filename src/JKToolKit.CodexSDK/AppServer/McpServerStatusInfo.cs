using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a single MCP server status entry as reported by Codex.
/// </summary>
public sealed record class McpServerStatusInfo
{
    /// <summary>
    /// Gets the MCP server name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the parsed auth status.
    /// </summary>
    public McpAuthStatus AuthStatus { get; init; }

    /// <summary>
    /// Gets server implementation metadata advertised by the MCP server, when present.
    /// </summary>
    public McpServerImplementationInfo? ServerInfo { get; init; }

    /// <summary>
    /// Gets the tools exposed by this server.
    /// </summary>
    public required IReadOnlyList<McpServerToolInfo> Tools { get; init; }

    /// <summary>
    /// Gets the resources exposed by this server.
    /// </summary>
    public required IReadOnlyList<McpServerResourceInfo> Resources { get; init; }

    /// <summary>
    /// Gets the resource templates exposed by this server.
    /// </summary>
    public required IReadOnlyList<McpServerResourceTemplateInfo> ResourceTemplates { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the server.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Presentation metadata advertised by an initialized MCP server.
/// </summary>
public sealed record class McpServerImplementationInfo
{
    /// <summary>
    /// Gets the server implementation name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the server implementation version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the optional display title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// Gets the raw server-info payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
