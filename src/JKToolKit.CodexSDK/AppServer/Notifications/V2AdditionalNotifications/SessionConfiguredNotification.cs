using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class SessionConfiguredNotification : AppServerNotification
{
    public string SessionId { get; }
    public string Model { get; }
    public string? ReasoningEffort { get; }
    public long HistoryLogId { get; }
    public int HistoryEntryCount { get; }
    public JsonElement? InitialMessages { get; }
    public string RolloutPath { get; }

    public SessionConfiguredNotification(
        string SessionId,
        string Model,
        string? ReasoningEffort,
        long HistoryLogId,
        int HistoryEntryCount,
        JsonElement? InitialMessages,
        string RolloutPath,
        JsonElement Params)
        : base("sessionConfigured", Params)
    {
        this.SessionId = SessionId;
        this.Model = Model;
        this.ReasoningEffort = ReasoningEffort;
        this.HistoryLogId = HistoryLogId;
        this.HistoryEntryCount = HistoryEntryCount;
        this.InitialMessages = InitialMessages;
        this.RolloutPath = RolloutPath;
    }
}

