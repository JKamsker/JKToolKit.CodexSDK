using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

public sealed record TurnCompletedNotification(
    string ThreadId,
    string TurnId,
    string Status,
    JsonElement? Error,
    JsonElement Params)
    : AppServerNotification("turn/completed", Params);

