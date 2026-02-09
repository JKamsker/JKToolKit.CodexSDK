using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the agent shares or updates its plan for a turn.
/// </summary>
public sealed record class TurnPlanUpdatedNotification : AppServerNotification
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
    /// Gets an optional explanation for the plan update.
    /// </summary>
    public string? Explanation { get; }

    /// <summary>
    /// Gets the updated plan steps.
    /// </summary>
    public IReadOnlyList<TurnPlanStep> Plan { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TurnPlanUpdatedNotification"/>.
    /// </summary>
    public TurnPlanUpdatedNotification(
        string ThreadId,
        string TurnId,
        string? Explanation,
        IReadOnlyList<TurnPlanStep> Plan,
        JsonElement Params)
        : base("turn/plan/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Explanation = Explanation;
        this.Plan = Plan ?? throw new ArgumentNullException(nameof(Plan));
    }
}
