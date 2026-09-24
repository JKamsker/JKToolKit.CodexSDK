using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when token usage information is updated for a thread/turn.
/// </summary>
public sealed record class ThreadTokenUsageUpdatedNotification : AppServerNotification
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
    /// Gets the raw token usage payload.
    /// </summary>
    public JsonElement TokenUsage { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadTokenUsageUpdatedNotification"/>.
    /// </summary>
    public ThreadTokenUsageUpdatedNotification(string ThreadId, string TurnId, JsonElement TokenUsage, JsonElement Params)
        : base(AppServerMethods.ThreadTokenUsageUpdated, Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.TokenUsage = TokenUsage;
    }
}
