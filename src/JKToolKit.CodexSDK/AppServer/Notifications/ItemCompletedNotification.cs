using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// Notification emitted when an item finishes processing within a turn.
/// </summary>
public sealed record class ItemCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Gets the raw item payload.
    /// </summary>
    public JsonElement Item { get; }

    /// <summary>
    /// Gets native content tokens for the completed assistant message, including a sampled footer, when available.
    /// This completion telemetry is not part of the stored thread item.
    /// </summary>
    public ulong? ContentTokenCount =>
        Params.ValueKind == JsonValueKind.Object &&
        Params.TryGetProperty("contentTokenCount", out var count) &&
        count.ValueKind == JsonValueKind.Number &&
        count.TryGetUInt64(out var value)
            ? value
            : null;

    /// <summary>
    /// Initializes a new instance of <see cref="ItemCompletedNotification"/>.
    /// </summary>
    public ItemCompletedNotification(string ThreadId, string TurnId, JsonElement Item, JsonElement Params)
        : base(AppServerMethods.ItemCompleted, Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Item = Item;
    }

    /// <summary>
    /// Gets the item identifier, if present in <see cref="Item"/>.
    /// </summary>
    public string? ItemId =>
        Item.ValueKind == JsonValueKind.Object &&
        Item.TryGetProperty(JsonFieldNames.Id, out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    /// <summary>
    /// Gets the item type discriminator, if present in <see cref="Item"/>.
    /// </summary>
    public string? ItemType =>
        Item.ValueKind == JsonValueKind.Object &&
        Item.TryGetProperty(JsonFieldNames.Type, out var t) &&
        t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;
}
