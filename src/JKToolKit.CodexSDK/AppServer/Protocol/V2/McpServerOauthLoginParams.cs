using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>mcpServer/oauth/login</c> request (v2 protocol).
/// </summary>
public sealed record class McpServerOauthLoginParams
{
    /// <summary>
    /// Gets the configured MCP server name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets optional OAuth scopes to request.
    /// </summary>
    [JsonPropertyName("scopes")]
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// Gets an optional login timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeoutSecs")]
    public long? TimeoutSecs { get; init; }
}

