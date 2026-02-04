using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class PlanDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }

    public PlanDeltaNotification(string ThreadId, string TurnId, string ItemId, string Delta, JsonElement Params)
        : base("item/plan/delta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}

