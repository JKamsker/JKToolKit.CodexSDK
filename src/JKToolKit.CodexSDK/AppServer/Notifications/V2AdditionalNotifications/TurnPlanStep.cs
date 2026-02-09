namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Represents a single plan step entry for a turn.
/// </summary>
public sealed record class TurnPlanStep
{
    /// <summary>
    /// Gets the step text.
    /// </summary>
    public string Step { get; }

    /// <summary>
    /// Gets the step status (for example: <c>pending</c>, <c>inProgress</c>, <c>completed</c>).
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TurnPlanStep"/>.
    /// </summary>
    public TurnPlanStep(string Step, string Status)
    {
        this.Step = Step;
        this.Status = Status;
    }
}
