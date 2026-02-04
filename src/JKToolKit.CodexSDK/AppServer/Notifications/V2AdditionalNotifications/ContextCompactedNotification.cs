using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ContextCompactedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }

    public ContextCompactedNotification(string ThreadId, string TurnId, JsonElement Params)
        : base("thread/compacted", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
    }
}

