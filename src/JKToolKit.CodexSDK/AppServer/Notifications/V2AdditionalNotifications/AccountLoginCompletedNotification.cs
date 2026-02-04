using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class AccountLoginCompletedNotification : AppServerNotification
{
    public string? LoginId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public AccountLoginCompletedNotification(string? LoginId, bool Success, string? Error, JsonElement Params)
        : base("account/login/completed", Params)
    {
        this.LoginId = LoginId;
        this.Success = Success;
        this.Error = Error;
    }
}

