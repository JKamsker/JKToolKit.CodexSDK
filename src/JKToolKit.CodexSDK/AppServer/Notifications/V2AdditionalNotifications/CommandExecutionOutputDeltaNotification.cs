using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class CommandExecutionOutputDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }

    public CommandExecutionOutputDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        JsonElement Params)
        : base("item/commandExecution/outputDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}

