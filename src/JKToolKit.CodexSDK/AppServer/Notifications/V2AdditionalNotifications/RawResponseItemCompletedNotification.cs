using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.ResponseItems;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an experimental raw response item completes.
/// </summary>
public sealed record class RawResponseItemCompletedNotification : AppServerNotification
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
    /// Gets the raw response item payload.
    /// </summary>
    public JsonElement Item { get; }

    /// <summary>
    /// Gets the typed parsed response item payload.
    /// </summary>
    public CodexResponseItem ResponseItem { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RawResponseItemCompletedNotification"/>.
    /// </summary>
    public RawResponseItemCompletedNotification(string ThreadId, string TurnId, JsonElement Item, JsonElement Params)
        : base("rawResponseItem/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Item = Item.Clone();
        this.ResponseItem = CodexResponseItemParser.Parse(this.Item);
    }
}
