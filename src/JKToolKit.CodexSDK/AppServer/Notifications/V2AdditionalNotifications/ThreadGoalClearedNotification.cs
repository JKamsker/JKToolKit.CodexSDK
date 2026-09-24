using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread goal is cleared.
/// </summary>
public sealed record class ThreadGoalClearedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadGoalClearedNotification"/>.
    /// </summary>
    public ThreadGoalClearedNotification(string threadId, JsonElement @params)
        : base(AppServerMethods.ThreadGoalCleared, @params)
    {
        ThreadId = threadId;
    }
}
