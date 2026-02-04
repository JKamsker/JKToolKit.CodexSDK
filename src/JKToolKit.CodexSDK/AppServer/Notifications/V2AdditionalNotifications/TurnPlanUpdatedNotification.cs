using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class TurnPlanUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string? Explanation { get; }
    public IReadOnlyList<TurnPlanStep> Plan { get; }

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

