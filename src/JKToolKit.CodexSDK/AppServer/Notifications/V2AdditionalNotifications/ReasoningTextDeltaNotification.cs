using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a <c>reasoning</c> item streams additional raw reasoning text.
/// </summary>
public sealed record class ReasoningTextDeltaNotification : AppServerNotification
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
    /// Gets the item identifier that the delta applies to.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the text delta to append for this item.
    /// </summary>
    public string Delta { get; }

    /// <summary>
    /// Gets the content index used to group deltas that belong together.
    /// </summary>
    public int ContentIndex { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ReasoningTextDeltaNotification"/>.
    /// </summary>
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
