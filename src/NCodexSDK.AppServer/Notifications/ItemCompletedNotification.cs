using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

public sealed record ItemCompletedNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    JsonElement Params)
    : AppServerNotification("item/completed", Params);

