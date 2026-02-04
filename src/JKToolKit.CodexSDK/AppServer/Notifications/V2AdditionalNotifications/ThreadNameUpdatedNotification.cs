using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ThreadNameUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string? ThreadName { get; }

    public ThreadNameUpdatedNotification(string ThreadId, string? ThreadName, JsonElement Params)
        : base("thread/name/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.ThreadName = ThreadName;
    }
}

