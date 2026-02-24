using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents an MCP server configuration entry as returned by <c>config/read</c>.
/// </summary>
public sealed record class McpServerConfigInfo
{
    /// <summary>
    /// Gets the server transport kind inferred from the config entry.
    /// </summary>
    public required string Transport { get; init; }

    /// <summary>
    /// Gets the stdio command when <see cref="Transport"/> is "stdio".
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// Gets optional stdio args when <see cref="Transport"/> is "stdio".
    /// </summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>
    /// Gets optional stdio env mapping when present.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>
    /// Gets optional env var names to inherit from the parent process.
    /// </summary>
    public IReadOnlyList<string>? EnvVars { get; init; }

    /// <summary>
    /// Gets optional working directory for stdio transport.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the streamable HTTP URL when <see cref="Transport"/> is "streamableHttp".
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets an optional bearer token env var name for streamable HTTP transport.
    /// </summary>
    public string? BearerTokenEnvVar { get; init; }

    /// <summary>
    /// Gets optional fixed HTTP headers for streamable HTTP transport.
    /// </summary>
    public IReadOnlyDictionary<string, string>? HttpHeaders { get; init; }

    /// <summary>
    /// Gets optional HTTP headers whose values are sourced from environment variables.
    /// </summary>
    public IReadOnlyDictionary<string, string>? EnvHttpHeaders { get; init; }

    /// <summary>
    /// Gets a value indicating whether this server is enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether this server is required.
    /// </summary>
    public bool? Required { get; init; }

    /// <summary>
    /// Gets an optional startup timeout.
    /// </summary>
    public TimeSpan? StartupTimeout { get; init; }

    /// <summary>
    /// Gets an optional tool call timeout.
    /// </summary>
    public TimeSpan? ToolTimeout { get; init; }

    /// <summary>
    /// Gets an optional tool allow-list.
    /// </summary>
    public IReadOnlyList<string>? EnabledTools { get; init; }

    /// <summary>
    /// Gets an optional tool deny-list.
    /// </summary>
    public IReadOnlyList<string>? DisabledTools { get; init; }

    /// <summary>
    /// Gets optional OAuth scopes for streamable HTTP transport.
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for forward compatibility.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

