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
    /// Gets the startup state reported by upstream, when present.
    /// </summary>
    public string? StartupStatus { get; init; }

    /// <summary>
    /// Gets the startup error text reported by upstream, when present.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the typed startup failure reason reported by upstream, when present.
    /// </summary>
    public McpServerStartupFailureReason? FailureReason { get; init; }

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
/// Represents a known MCP server startup failure reason.
/// </summary>
public readonly record struct McpServerStartupFailureReason
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private McpServerStartupFailureReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MCP server startup failure reason cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>reauthenticationRequired</c> startup failure reason.
    /// </summary>
    public static McpServerStartupFailureReason ReauthenticationRequired => new("reauthenticationRequired");

    /// <summary>
    /// Parses a startup failure reason from a wire value.
    /// </summary>
    public static McpServerStartupFailureReason Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a startup failure reason from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out McpServerStartupFailureReason reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = default;
            return false;
        }

        reason = new McpServerStartupFailureReason(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="McpServerStartupFailureReason"/>.
    /// </summary>
    public static implicit operator McpServerStartupFailureReason(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="McpServerStartupFailureReason"/> to its wire value.
    /// </summary>
    public static implicit operator string(McpServerStartupFailureReason reason) => reason.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
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
