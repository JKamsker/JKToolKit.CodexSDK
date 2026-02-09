using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the aggregated unified diff for a turn is updated.
/// </summary>
public sealed record class TurnDiffUpdatedNotification : AppServerNotification
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
    /// Gets the latest aggregated unified diff text.
    /// </summary>
    public string Diff { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TurnDiffUpdatedNotification"/>.
    /// </summary>
    public TurnDiffUpdatedNotification(string ThreadId, string TurnId, string Diff, JsonElement Params)
        : base("turn/diff/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Diff = Diff;
    }
}
