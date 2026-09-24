using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an app-server project is created, updated, or deleted.
/// </summary>
public sealed record class ProjectChangedNotification(
    string ProjectId,
    string ChangeType,
    JsonElement Params) : AppServerNotification(AppServerMethods.ProjectChanged, Params);
