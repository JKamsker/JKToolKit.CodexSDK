using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an app-server project is created, updated, or deleted.
/// </summary>
public sealed record class ProjectChangedNotification(
    string ProjectId,
    string ChangeType,
    JsonElement Params) : AppServerNotification("project/changed", Params);
