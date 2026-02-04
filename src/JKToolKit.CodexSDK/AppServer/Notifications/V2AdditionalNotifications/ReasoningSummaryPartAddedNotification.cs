using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ReasoningSummaryPartAddedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public int SummaryIndex { get; }

    public ReasoningSummaryPartAddedNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        int SummaryIndex,
        JsonElement Params)
        : base("item/reasoning/summaryPartAdded", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.SummaryIndex = SummaryIndex;
    }
}

