using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an MCP server startup status changes.
/// </summary>
public sealed record class McpServerStartupStatusUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the MCP server name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the startup status value.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets an optional startup error when status indicates failure.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="McpServerStartupStatusUpdatedNotification"/>.
    /// </summary>
    public McpServerStartupStatusUpdatedNotification(string Name, string Status, string? Error, JsonElement Params)
        : base("mcpServer/startupStatus/updated", Params)
    {
        this.Name = Name;
        this.Status = Status;
        this.Error = Error;
    }
}
