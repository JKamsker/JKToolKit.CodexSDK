using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ErrorNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement Error { get; }
    public bool WillRetry { get; }

    public ErrorNotification(string ThreadId, string TurnId, JsonElement Error, bool WillRetry, JsonElement Params)
        : base("error", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Error = Error;
        this.WillRetry = WillRetry;
    }
}

