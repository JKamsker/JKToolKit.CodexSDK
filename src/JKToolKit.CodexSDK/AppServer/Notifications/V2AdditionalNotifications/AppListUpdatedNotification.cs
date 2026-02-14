using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server's apps/connectors list changes.
/// </summary>
public sealed record class AppListUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the raw apps payload, if present.
    /// </summary>
    public JsonElement Apps { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AppListUpdatedNotification"/>.
    /// </summary>
    public AppListUpdatedNotification(JsonElement Apps, JsonElement Params)
        : base("app/list/updated", Params)
    {
        this.Apps = Apps;
    }
}

