using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an upstream raw response completes.
/// </summary>
public sealed record class RawResponseCompletedNotification : AppServerNotification
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
    /// Gets the raw response identifier.
    /// </summary>
    public string ResponseId { get; }

    /// <summary>
    /// Gets the raw usage payload, when present.
    /// </summary>
    public JsonElement? Usage { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RawResponseCompletedNotification"/>.
    /// </summary>
    public RawResponseCompletedNotification(string ThreadId, string TurnId, string ResponseId, JsonElement? Usage, JsonElement Params)
        : base("rawResponse/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ResponseId = ResponseId;
        this.Usage = Usage;
    }
}
