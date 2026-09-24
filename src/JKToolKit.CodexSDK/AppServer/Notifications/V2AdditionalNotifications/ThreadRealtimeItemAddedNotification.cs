using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted when a raw thread realtime item is emitted by the backend.
/// </summary>
public sealed record class ThreadRealtimeItemAddedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the raw realtime item payload.
    /// </summary>
    public JsonElement Item { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeItemAddedNotification"/>.
    /// </summary>
    public ThreadRealtimeItemAddedNotification(string ThreadId, JsonElement Item, JsonElement Params)
        : base(AppServerMethods.ThreadRealtimeItemAdded, Params)
    {
        this.ThreadId = ThreadId;
        this.Item = Item;
    }

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
