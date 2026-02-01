using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record UnknownNotification(string Method, JsonElement Params) : AppServerNotification(Method, Params);

