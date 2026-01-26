using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

public sealed record ItemStartedNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    JsonElement Params)
    : AppServerNotification("item/started", Params);

