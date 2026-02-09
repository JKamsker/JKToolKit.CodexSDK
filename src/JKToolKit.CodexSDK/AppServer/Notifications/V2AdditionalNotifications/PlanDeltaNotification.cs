using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a <c>plan</c> item streams additional text (experimental).
/// </summary>
public sealed record class PlanDeltaNotification : AppServerNotification
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
    /// Gets the plan text delta to append for this item.
    /// </summary>
    public string Delta { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PlanDeltaNotification"/>.
    /// </summary>
    public PlanDeltaNotification(string ThreadId, string TurnId, string ItemId, string Delta, JsonElement Params)
        : base("item/plan/delta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}
