using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record ItemStartedNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    JsonElement Params)
    : AppServerNotification("item/started", Params);

