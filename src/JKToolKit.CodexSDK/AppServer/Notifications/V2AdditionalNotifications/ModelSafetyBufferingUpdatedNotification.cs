namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when model safety buffering state changes.
/// </summary>
public sealed record class ModelSafetyBufferingUpdatedNotification : AppServerNotification
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
    /// Gets the model associated with the buffering update.
    /// </summary>
    public string? Model { get; }

    /// <summary>
    /// Gets the use cases payload.
    /// </summary>
    public System.Text.Json.JsonElement UseCases { get; }

    /// <summary>
    /// Gets the reasons payload.
    /// </summary>
    public System.Text.Json.JsonElement Reasons { get; }

    /// <summary>
    /// Gets a value indicating whether clients should show buffering UI.
    /// </summary>
    public bool ShowBufferingUi { get; }

    /// <summary>
    /// Gets the faster model hint, if present.
    /// </summary>
    public string? FasterModel { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ModelSafetyBufferingUpdatedNotification"/>.
    /// </summary>
    public ModelSafetyBufferingUpdatedNotification(
        string ThreadId,
        string TurnId,
        string? Model,
        System.Text.Json.JsonElement UseCases,
        System.Text.Json.JsonElement Reasons,
        bool ShowBufferingUi,
        string? FasterModel,
        System.Text.Json.JsonElement Params)
        : base("model/safetyBuffering/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Model = Model;
        this.UseCases = UseCases;
        this.Reasons = Reasons;
        this.ShowBufferingUi = ShowBufferingUi;
        this.FasterModel = FasterModel;
    }
}
