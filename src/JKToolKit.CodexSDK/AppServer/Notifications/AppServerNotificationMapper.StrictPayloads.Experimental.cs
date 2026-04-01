using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static ThreadRealtimeStartedNotification? TryMapThreadRealtimeStarted(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "version", out var version))
        {
            return null;
        }

        if (!TryGetOptionalString(p, "sessionId", out var sessionId))
        {
            return null;
        }

        return new ThreadRealtimeStartedNotification(threadId, sessionId, version, p);
    }

    private static ThreadRealtimeItemAddedNotification? TryMapThreadRealtimeItemAdded(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredElement(p, "item", out var item))
        {
            return null;
        }

        return new ThreadRealtimeItemAddedNotification(threadId, item, p);
    }

    private static ThreadRealtimeOutputAudioDeltaNotification? TryMapThreadRealtimeOutputAudioDelta(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredObject(p, "audio", out var audio) ||
            !TryGetRequiredString(audio, "data", out _) ||
            !TryGetRequiredNonNegativeInt32(audio, "numChannels", out _) ||
            !TryGetRequiredNonNegativeInt32(audio, "sampleRate", out _) ||
            !TryGetOptionalString(audio, "itemId", out _) ||
            !TryGetOptionalNonNegativeInt32(audio, "samplesPerChannel", out _))
        {
            return null;
        }

        return new ThreadRealtimeOutputAudioDeltaNotification(threadId, audio, p);
    }

    private static ThreadRealtimeErrorNotification? TryMapThreadRealtimeError(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "message", out var message))
        {
            return null;
        }

        return new ThreadRealtimeErrorNotification(threadId, message, p);
    }

    private static ThreadRealtimeClosedNotification? TryMapThreadRealtimeClosed(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetOptionalString(p, "reason", out var reason))
        {
            return null;
        }

        return new ThreadRealtimeClosedNotification(threadId, reason, p);
    }

    private static HookStartedNotification? TryMapHookStarted(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetOptionalString(p, "turnId", out var turnId) ||
            !AppServerNotificationParsing.TryParseHookRunSummaryInfo(p, "run", out var runInfo, out var run))
        {
            return null;
        }

        return new HookStartedNotification(threadId, turnId, run, runInfo, p);
    }

    private static HookCompletedNotification? TryMapHookCompleted(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetOptionalString(p, "turnId", out var turnId) ||
            !AppServerNotificationParsing.TryParseHookRunSummaryInfo(p, "run", out var runInfo, out var run))
        {
            return null;
        }

        return new HookCompletedNotification(threadId, turnId, run, runInfo, p);
    }

    private static CommandExecutionOutputDeltaNotification? TryMapCommandExecutionOutputDelta(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "turnId", out var turnId) ||
            !TryGetRequiredString(p, "itemId", out var itemId) ||
            !TryGetRequiredString(p, "delta", out var delta))
        {
            return null;
        }

        return new CommandExecutionOutputDeltaNotification(threadId, turnId, itemId, delta, p);
    }

    private static ItemAutoApprovalReviewStartedNotification? TryMapAutoApprovalReviewStarted(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "turnId", out var turnId) ||
            !TryGetRequiredString(p, "targetItemId", out var targetItemId) ||
            !AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(p, "review", out var review))
        {
            return null;
        }

        return new ItemAutoApprovalReviewStartedNotification(
            threadId,
            turnId,
            targetItemId,
            GetOptionalAny(p, "action") ?? EmptyObject(),
            review,
            p);
    }

    private static ItemAutoApprovalReviewCompletedNotification? TryMapAutoApprovalReviewCompleted(JsonElement p)
    {
        if (!TryGetRequiredString(p, "threadId", out var threadId) ||
            !TryGetRequiredString(p, "turnId", out var turnId) ||
            !TryGetRequiredString(p, "targetItemId", out var targetItemId) ||
            !AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(p, "review", out var review))
        {
            return null;
        }

        return new ItemAutoApprovalReviewCompletedNotification(
            threadId,
            turnId,
            targetItemId,
            GetOptionalAny(p, "action") ?? EmptyObject(),
            review,
            p);
    }

    private static bool TryGetRequiredObject(JsonElement obj, string propertyName, out JsonElement value)
    {
        value = default;
        return obj.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGetRequiredElement(JsonElement obj, string propertyName, out JsonElement value)
    {
        value = default;
        return obj.TryGetProperty(propertyName, out value) && value.ValueKind != JsonValueKind.Undefined;
    }

    private static bool TryGetOptionalString(JsonElement obj, string propertyName, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString();
        return true;
    }

    private static bool TryGetRequiredNonNegativeInt32(JsonElement obj, string propertyName, out int value)
    {
        value = default;
        return obj.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number &&
               prop.TryGetInt32(out value) &&
               value >= 0;
    }

    private static bool TryGetOptionalNonNegativeInt32(JsonElement obj, string propertyName, out int? value)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (prop.ValueKind == JsonValueKind.Number &&
            prop.TryGetInt32(out var parsed) &&
            parsed >= 0)
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
