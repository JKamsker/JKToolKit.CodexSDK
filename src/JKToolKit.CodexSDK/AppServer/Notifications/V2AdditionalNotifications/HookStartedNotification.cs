using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a configured hook starts running.
/// </summary>
public sealed record class HookStartedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier when the hook is scoped to a turn.
    /// </summary>
    public string? TurnId { get; }

    /// <summary>
    /// Gets the raw hook run summary payload.
    /// </summary>
    public JsonElement Run { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HookStartedNotification"/>.
    /// </summary>
    public HookStartedNotification(string ThreadId, string? TurnId, JsonElement Run, JsonElement Params)
        : base("hook/started", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Run = Run;
    }
}
