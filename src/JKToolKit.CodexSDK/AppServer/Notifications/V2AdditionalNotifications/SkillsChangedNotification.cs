using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

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
        : base(AppServerMethods.SkillsChanged, @params)
    {
    }
}
