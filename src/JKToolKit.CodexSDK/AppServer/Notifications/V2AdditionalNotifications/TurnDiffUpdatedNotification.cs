using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class TurnDiffUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string Diff { get; }

    public TurnDiffUpdatedNotification(string ThreadId, string TurnId, string Diff, JsonElement Params)
        : base("turn/diff/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Diff = Diff;
    }
}

