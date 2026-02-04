using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class RawResponseItemCompletedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement Item { get; }

    public RawResponseItemCompletedNotification(string ThreadId, string TurnId, JsonElement Item, JsonElement Params)
        : base("rawResponseItem/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Item = Item;
    }
}

