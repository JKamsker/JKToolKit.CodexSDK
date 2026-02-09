using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a running command execution requests terminal interaction (stdin).
/// </summary>
public sealed record class TerminalInteractionNotification : AppServerNotification
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
    /// Gets the item identifier (command execution item).
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the process identifier for the running command.
    /// </summary>
    public string ProcessId { get; }

    /// <summary>
    /// Gets the stdin prompt or input request text.
    /// </summary>
    public string Stdin { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TerminalInteractionNotification"/>.
    /// </summary>
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
