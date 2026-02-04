using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public abstract record class AppServerNotification
{
    public string Method { get; }
    public JsonElement Params { get; }

    protected AppServerNotification(string method, JsonElement @params)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Params = @params;
    }
}

