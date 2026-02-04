using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class LoginChatGptCompleteNotification : AppServerNotification
{
    public string LoginId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public LoginChatGptCompleteNotification(string LoginId, bool Success, string? Error, JsonElement Params)
        : base("loginChatGptComplete", Params)
    {
        this.LoginId = LoginId;
        this.Success = Success;
        this.Error = Error;
    }
}

