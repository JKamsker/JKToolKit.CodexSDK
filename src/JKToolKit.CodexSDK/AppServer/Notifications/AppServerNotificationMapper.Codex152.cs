using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static AppServerNotification? TryMapCodex152Notification(string method, JsonElement p) =>
        method switch
        {
            "modelProvider/authRecoveryStarted" => TryMapModelProviderAuthRecovery(method, p),
            "modelProvider/authRecoveryCompleted" => TryMapModelProviderAuthRecovery(method, p),
            _ => null
        };
}
