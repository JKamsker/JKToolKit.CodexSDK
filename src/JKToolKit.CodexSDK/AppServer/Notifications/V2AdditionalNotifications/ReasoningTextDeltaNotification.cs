using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ReasoningTextDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }
    public int ContentIndex { get; }

    public ReasoningTextDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        int ContentIndex,
        JsonElement Params)
        : base("item/reasoning/textDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
        this.ContentIndex = ContentIndex;
    }
}

