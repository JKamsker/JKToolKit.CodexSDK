using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a <c>reasoning</c> item streams additional summary text.
/// </summary>
public sealed record class ReasoningSummaryTextDeltaNotification : AppServerNotification
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
    /// Gets the summary text delta to append for this item.
    /// </summary>
    public string Delta { get; }

    /// <summary>
    /// Gets the summary index that identifies the current summary section.
    /// </summary>
    public int SummaryIndex { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ReasoningSummaryTextDeltaNotification"/>.
    /// </summary>
    public ReasoningSummaryTextDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        int SummaryIndex,
        JsonElement Params)
        : base("item/reasoning/summaryTextDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
        this.SummaryIndex = SummaryIndex;
    }
}
