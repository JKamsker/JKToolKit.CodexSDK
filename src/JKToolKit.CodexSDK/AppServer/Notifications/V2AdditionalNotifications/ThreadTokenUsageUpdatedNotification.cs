using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ThreadTokenUsageUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement TokenUsage { get; }

    public ThreadTokenUsageUpdatedNotification(string ThreadId, string TurnId, JsonElement TokenUsage, JsonElement Params)
        : base("thread/tokenUsage/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.TokenUsage = TokenUsage;
    }
}

