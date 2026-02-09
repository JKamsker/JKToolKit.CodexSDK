using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an MCP server OAuth login flow completes.
/// </summary>
public sealed record class McpServerOauthLoginCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the MCP server name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the login succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets an optional error message if <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="McpServerOauthLoginCompletedNotification"/>.
    /// </summary>
    public McpServerOauthLoginCompletedNotification(string Name, bool Success, string? Error, JsonElement Params)
        : base("mcpServer/oauthLogin/completed", Params)
    {
        this.Name = Name;
        this.Success = Success;
        this.Error = Error;
    }
}
