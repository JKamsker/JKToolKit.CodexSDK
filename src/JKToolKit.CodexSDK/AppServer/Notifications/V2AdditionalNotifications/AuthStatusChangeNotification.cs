using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class AuthStatusChangeNotification : AppServerNotification
{
    public string? AuthMethod { get; }

    public AuthStatusChangeNotification(string? AuthMethod, JsonElement Params)
        : base("authStatusChange", Params)
    {
        this.AuthMethod = AuthMethod;
    }
}

