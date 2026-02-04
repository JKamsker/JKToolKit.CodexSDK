using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ReasoningSummaryTextDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }
    public int SummaryIndex { get; }

    public ReasoningSummaryTextDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        int SummaryIndex,
        JsonElement Params)
        : base("item/reasoning/summaryTextDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
        this.SummaryIndex = SummaryIndex;
    }
}

