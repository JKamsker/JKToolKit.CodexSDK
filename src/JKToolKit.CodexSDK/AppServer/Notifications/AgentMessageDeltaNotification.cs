using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// Notification emitted when an <c>agentMessage</c> item streams additional text.
/// </summary>
public sealed record class AgentMessageDeltaNotification : AppServerNotification
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
    /// Initializes a new instance of <see cref="AgentMessageDeltaNotification"/>.
    /// </summary>
    public AgentMessageDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        JsonElement Params)
        : base("item/agentMessage/delta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}

