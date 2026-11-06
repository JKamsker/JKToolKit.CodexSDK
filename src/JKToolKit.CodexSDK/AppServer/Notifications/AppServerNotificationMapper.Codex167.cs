using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static AppServerNotification? TryMapCodex167Notification(string method, JsonElement p) =>
        method switch
        {
            AppServerMethods.ThreadSectionChanged => TryMapThreadSectionChanged(p),
            _ => null
        };

    private static AppServerNotification? TryMapThreadSectionChanged(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !p.TryGetProperty("sectionId", out var sectionIdElement) ||
            sectionIdElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            return null;
        }

        return new ThreadSectionChangedNotification(
            ThreadId: threadId,
            SectionId: sectionIdElement.ValueKind == JsonValueKind.String ? sectionIdElement.GetString() : null,
            Params: p);
    }
}
