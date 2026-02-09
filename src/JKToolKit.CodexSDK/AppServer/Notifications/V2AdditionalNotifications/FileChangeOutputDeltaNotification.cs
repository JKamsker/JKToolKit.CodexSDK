using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a <c>fileChange</c> item streams additional tool output.
/// </summary>
public sealed record class FileChangeOutputDeltaNotification : AppServerNotification
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
    /// Gets the output delta to append for this item.
    /// </summary>
    public string Delta { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="FileChangeOutputDeltaNotification"/>.
    /// </summary>
    public FileChangeOutputDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        JsonElement Params)
        : base("item/fileChange/outputDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}
