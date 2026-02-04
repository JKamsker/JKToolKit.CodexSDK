using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class UnknownNotification : AppServerNotification
{
    public UnknownNotification(string method, JsonElement @params)
        : base(method, @params)
    {
    }
}

