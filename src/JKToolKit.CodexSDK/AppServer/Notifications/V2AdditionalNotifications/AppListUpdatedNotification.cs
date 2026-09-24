using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server's apps/connectors list changes.
/// </summary>
public sealed record class AppListUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the typed apps payload, if present.
    /// </summary>
    public IReadOnlyList<AppDescriptor> Apps { get; }

    /// <summary>
    /// Gets the raw <c>data</c> payload, if present.
    /// </summary>
    public JsonElement Data { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AppListUpdatedNotification"/>.
    /// </summary>
    public AppListUpdatedNotification(IReadOnlyList<AppDescriptor> apps, JsonElement data, JsonElement @params)
        : base(AppServerMethods.AppListUpdated, @params)
    {
        Apps = apps;
        Data = data;
    }
}

