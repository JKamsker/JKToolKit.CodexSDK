using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server reports an error condition.
/// </summary>
public sealed record class ErrorNotification : AppServerNotification
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
    /// Gets the raw error payload.
    /// </summary>
    public JsonElement Error { get; }

    /// <summary>
    /// Gets a value indicating whether the server intends to retry the failing operation.
    /// </summary>
    public bool WillRetry { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ErrorNotification"/>.
    /// </summary>
    public ErrorNotification(string ThreadId, string TurnId, JsonElement Error, bool WillRetry, JsonElement Params)
        : base("error", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Error = Error;
        this.WillRetry = WillRetry;
    }
}
