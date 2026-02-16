using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of starting an MCP server OAuth login flow.
/// </summary>
public sealed record class McpServerOauthLoginResult
{
    /// <summary>
    /// Gets the authorization URL to open in a browser.
    /// </summary>
    public required string AuthorizationUrl { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

