using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class TerminalInteractionNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string ProcessId { get; }
    public string Stdin { get; }

    public TerminalInteractionNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string ProcessId,
        string Stdin,
        JsonElement Params)
        : base("item/commandExecution/terminalInteraction", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.ProcessId = ProcessId;
        this.Stdin = Stdin;
    }
}

