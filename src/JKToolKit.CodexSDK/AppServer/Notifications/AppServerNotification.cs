using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public abstract record AppServerNotification(string Method, JsonElement Params);

