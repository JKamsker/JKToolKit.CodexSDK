using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a configured hook finishes running.
/// </summary>
public sealed record class HookCompletedNotification : AppServerNotification
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
    /// Gets the typed hook run summary.
    /// </summary>
    public HookRunSummaryInfo RunInfo { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HookCompletedNotification"/>.
    /// </summary>
    public HookCompletedNotification(string ThreadId, string? TurnId, JsonElement Run, HookRunSummaryInfo RunInfo, JsonElement Params)
        : base("hook/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Run = Run;
        this.RunInfo = RunInfo;
    }
}
