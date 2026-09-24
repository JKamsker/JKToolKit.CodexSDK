using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread goal is set or updated.
/// </summary>
public sealed record class ThreadGoalUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier associated with the update, when present.
    /// </summary>
    public string? TurnId { get; }

    /// <summary>
    /// Gets the parsed goal payload.
    /// </summary>
    public ThreadGoal? Goal { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadGoalUpdatedNotification"/>.
    /// </summary>
    public ThreadGoalUpdatedNotification(string threadId, string? turnId, ThreadGoal? goal, JsonElement @params)
        : base(AppServerMethods.ThreadGoalUpdated, @params)
    {
        ThreadId = threadId;
        TurnId = turnId;
        Goal = goal;
    }
}
