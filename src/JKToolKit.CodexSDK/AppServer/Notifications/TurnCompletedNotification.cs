using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record TurnCompletedNotification(
    string ThreadId,
    string TurnId,
    string Status,
    JsonElement? Error,
    JsonElement Params)
    : AppServerNotification("turn/completed", Params);

