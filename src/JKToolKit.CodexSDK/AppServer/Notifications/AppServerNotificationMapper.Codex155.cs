using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static AppServerNotification? TryMapCodex155Notification(string method, JsonElement p) =>
        method switch
        {
            "thread/attachment/updated" => TryMapThreadAttachmentUpdated(p),
            _ => null
        };

    private static AppServerNotification? TryMapThreadAttachmentUpdated(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "attachmentType", out var attachmentType) ||
            !TryGetRequiredString(p, "identityKey", out var identityKey) ||
            !TryGetRequiredString(p, "attachmentId", out var attachmentId) ||
            !TryGetRequiredString(p, "operation", out var operationValue))
        {
            return null;
        }

        var operation = operationValue switch
        {
            "created" => ThreadAttachmentOperation.Created,
            "deleted" => ThreadAttachmentOperation.Deleted,
            _ => ThreadAttachmentOperation.Unknown
        };

        if (operation == ThreadAttachmentOperation.Unknown)
        {
            return null;
        }

        return new ThreadAttachmentUpdatedNotification(
            threadId,
            attachmentType,
            identityKey,
            attachmentId,
            operation,
            p);
    }
}
