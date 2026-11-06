using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted after a thread moves into, within, or out of a section.
/// </summary>
public sealed record class ThreadSectionChangedNotification(
    string ThreadId,
    string? SectionId,
    JsonElement Params) : AppServerNotification(AppServerMethods.ThreadSectionChanged, Params);
