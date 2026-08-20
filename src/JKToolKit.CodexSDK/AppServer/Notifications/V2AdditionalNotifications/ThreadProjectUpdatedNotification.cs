using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread's project assignment changes.
/// </summary>
public sealed record class ThreadProjectUpdatedNotification(
    string ThreadId,
    string? ProjectId,
    JsonElement Params) : AppServerNotification("thread/project/updated", Params);
