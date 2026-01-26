using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

public abstract record AppServerNotification(string Method, JsonElement Params);

