using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record ItemStartedNotification(
    string ThreadId,
    string TurnId,
    JsonElement Item,
    JsonElement Params)
    : AppServerNotification("item/started", Params)
{
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


