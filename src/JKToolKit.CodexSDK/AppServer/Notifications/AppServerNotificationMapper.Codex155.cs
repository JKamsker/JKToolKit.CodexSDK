using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static AppServerNotification? TryMapCodex155Notification(string method, JsonElement p) =>
        method switch
        {
            AppServerMethods.ThreadAttachmentUpdated => TryMapThreadAttachmentUpdated(p),
            _ => null
        };

    private static AppServerNotification? TryMapThreadAttachmentUpdated(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.AttachmentType, out var attachmentType) ||
            !TryGetRequiredString(p, JsonFieldNames.IdentityKey, out var identityKey) ||
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
