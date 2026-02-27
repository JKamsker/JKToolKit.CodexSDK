using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the backend reroutes a request to a different model.
/// </summary>
public sealed record class ModelReroutedNotification : AppServerNotification
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
    /// Gets the originally selected model.
    /// </summary>
    public string FromModel { get; }

    /// <summary>
    /// Gets the new model.
    /// </summary>
    public string ToModel { get; }

    /// <summary>
    /// Gets the reroute reason.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ModelReroutedNotification"/>.
    /// </summary>
    public ModelReroutedNotification(string ThreadId, string TurnId, string FromModel, string ToModel, string Reason, JsonElement Params)
        : base("model/rerouted", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.FromModel = FromModel;
        this.ToModel = ToModel;
        this.Reason = Reason;
    }
}
