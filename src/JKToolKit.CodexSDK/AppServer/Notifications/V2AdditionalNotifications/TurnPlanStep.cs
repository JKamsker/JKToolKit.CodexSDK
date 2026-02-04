namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class TurnPlanStep
{
    public string Step { get; }
    public string Status { get; }

    public TurnPlanStep(string Step, string Status)
    {
        this.Step = Step;
        this.Status = Status;
    }
}

