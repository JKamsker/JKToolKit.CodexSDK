using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record ItemCompletedNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    JsonElement Params)
    : AppServerNotification("item/completed", Params);

