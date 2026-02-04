using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ThreadStartedNotification : AppServerNotification
{
    public JsonElement Thread { get; }

    public ThreadStartedNotification(JsonElement Thread, JsonElement Params)
        : base("thread/started", Params)
    {
        this.Thread = Thread;
    }

    public string? ThreadId =>
        Thread.ValueKind == JsonValueKind.Object &&
        Thread.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}

