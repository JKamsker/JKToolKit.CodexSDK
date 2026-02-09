using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a new reasoning summary section begins.
/// </summary>
public sealed record class ReasoningSummaryPartAddedNotification : AppServerNotification
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
    /// Gets the item identifier.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the new summary index.
    /// </summary>
    public int SummaryIndex { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ReasoningSummaryPartAddedNotification"/>.
    /// </summary>
    public ReasoningSummaryPartAddedNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        int SummaryIndex,
        JsonElement Params)
        : base("item/reasoning/summaryPartAdded", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.SummaryIndex = SummaryIndex;
    }
}
