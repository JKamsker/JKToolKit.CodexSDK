using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a model response enters safety buffering.
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
    /// Gets the model currently in safety buffering.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Gets upstream safety-buffering use-case labels.
    /// </summary>
    public IReadOnlyList<string> UseCases { get; }

    /// <summary>
    /// Gets upstream safety-buffering reason labels.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; }

    /// <summary>
    /// Gets a value indicating whether clients should show safety-buffering UI.
    /// </summary>
    public bool ShowBufferingUi { get; }

    /// <summary>
    /// Gets the faster model suggested by upstream, when present.
    /// </summary>
    public string? FasterModel { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ModelSafetyBufferingUpdatedNotification"/>.
    /// </summary>
    public ModelSafetyBufferingUpdatedNotification(
        string ThreadId,
        string TurnId,
        string Model,
        IReadOnlyList<string> UseCases,
        IReadOnlyList<string> Reasons,
        bool ShowBufferingUi,
        string? FasterModel,
        JsonElement Params)
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
