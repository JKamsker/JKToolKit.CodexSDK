using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// Notification emitted when a new item begins processing within a turn.
/// </summary>
public sealed record class ItemStartedNotification : AppServerNotification
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
    /// Initializes a new instance of <see cref="ItemStartedNotification"/>.
    /// </summary>
    public ItemStartedNotification(string ThreadId, string TurnId, JsonElement Item, JsonElement Params)
        : base(AppServerMethods.ItemStarted, Params)
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

