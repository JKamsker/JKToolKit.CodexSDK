using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when watched local skills change.
/// </summary>
public sealed record class SkillsChangedNotification : AppServerNotification
{
    /// <summary>
    /// Initializes a new instance of <see cref="SkillsChangedNotification"/>.
    /// </summary>
    public SkillsChangedNotification(JsonElement @params)
        : base("skills/changed", @params)
    {
    }
}
