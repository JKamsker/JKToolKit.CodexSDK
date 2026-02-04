using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class AccountUpdatedNotification : AppServerNotification
{
    public string? AuthMode { get; }

    public AccountUpdatedNotification(string? AuthMode, JsonElement Params)
        : base("account/updated", Params)
    {
        this.AuthMode = AuthMode;
    }
}

