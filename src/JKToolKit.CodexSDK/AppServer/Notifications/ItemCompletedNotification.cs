using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ItemCompletedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement Item { get; }

    public ItemCompletedNotification(string ThreadId, string TurnId, JsonElement Item, JsonElement Params)
        : base("item/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Item = Item;
    }

    public string? ItemId =>
        Item.ValueKind == JsonValueKind.Object &&
        Item.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    public string? ItemType =>
        Item.ValueKind == JsonValueKind.Object &&
        Item.TryGetProperty("type", out var t) &&
        t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;
}


