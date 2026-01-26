using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

public sealed record UnknownNotification(string Method, JsonElement Params) : AppServerNotification(Method, Params);

