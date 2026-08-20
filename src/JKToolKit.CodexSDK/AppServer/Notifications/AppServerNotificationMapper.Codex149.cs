using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static AppServerNotification? TryMapCodex149Notification(string method, JsonElement p) =>
        method switch
        {
            "thread/project/updated" => TryMapThreadProjectUpdated(p),
            "project/changed" => TryMapProjectChanged(p),
            "autoApprovalReview/strictReviewRequired" => TryMapStrictReviewRequired(p),
            _ => null
        };

    private static AppServerNotification? TryMapThreadProjectUpdated(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetOptionalString(p, "projectId", out var projectId))
        {
            return null;
        }

        return new ThreadProjectUpdatedNotification(threadId, projectId, p);
    }

    private static AppServerNotification? TryMapProjectChanged(JsonElement p)
    {
        if (!TryGetRequiredString(p, "projectId", out var projectId) ||
            !TryGetRequiredString(p, "changeType", out var changeType))
        {
            return null;
        }

        return new ProjectChangedNotification(projectId, changeType, p);
    }

    private static AppServerNotification? TryMapStrictReviewRequired(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "turnId", out var turnId) ||
            !TryGetRequiredInt64(p, "startedAtMs", out var startedAtMs))
        {
            return null;
        }

        return new StrictReviewRequiredNotification(threadId, turnId, startedAtMs, p);
    }

    private static bool TryGetRequiredInt64(JsonElement obj, string propertyName, out long value)
    {
        value = default;

        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out value))
        {
            return true;
        }

        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out value))
        {
            return true;
        }

        return false;
    }
}
