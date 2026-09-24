using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static ThreadRealtimeStartedNotification? TryMapThreadRealtimeStarted(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.Version, out var version))
        {
            return null;
        }

        if (!TryGetOptionalString(p, JsonFieldNames.SessionId, out var sessionId))
        {
            return null;
        }

        return new ThreadRealtimeStartedNotification(threadId, sessionId, version, p);
    }

    private static ThreadRealtimeItemAddedNotification? TryMapThreadRealtimeItemAdded(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredElement(p, JsonFieldNames.Item, out var item))
        {
            return null;
        }

        return new ThreadRealtimeItemAddedNotification(threadId, item, p);
    }

    private static ThreadRealtimeOutputAudioDeltaNotification? TryMapThreadRealtimeOutputAudioDelta(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredObject(p, JsonFieldNames.Audio, out var audio) ||
            !TryGetRequiredString(audio, JsonFieldNames.Data, out _) ||
            !TryGetRequiredNonNegativeInt32(audio, JsonFieldNames.NumChannels, out _) ||
            !TryGetRequiredNonNegativeInt32(audio, JsonFieldNames.SampleRate, out _) ||
            !TryGetOptionalString(audio, JsonFieldNames.ItemId, out _) ||
            !TryGetOptionalNonNegativeInt32(audio, JsonFieldNames.SamplesPerChannel, out _))
        {
            return null;
        }

        return new ThreadRealtimeOutputAudioDeltaNotification(threadId, audio, p);
    }

    private static ThreadRealtimeSdpNotification? TryMapThreadRealtimeSdp(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, "sdp", out var sdp))
        {
            return null;
        }

        return new ThreadRealtimeSdpNotification(threadId, sdp, p);
    }

    private static ThreadRealtimeErrorNotification? TryMapThreadRealtimeError(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.Message, out var message))
        {
            return null;
        }

        return new ThreadRealtimeErrorNotification(threadId, message, p);
    }

    private static ThreadRealtimeClosedNotification? TryMapThreadRealtimeClosed(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetOptionalString(p, JsonFieldNames.Reason, out var reason))
        {
            return null;
        }

        return new ThreadRealtimeClosedNotification(threadId, reason, p);
    }

    private static HookStartedNotification? TryMapHookStarted(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetOptionalString(p, JsonFieldNames.TurnId, out var turnId) ||
            !AppServerNotificationParsing.TryParseHookRunSummaryInfo(p, JsonFieldNames.Run, out var runInfo, out var run))
        {
            return null;
        }

        return new HookStartedNotification(threadId, turnId, run, runInfo, p);
    }

    private static HookCompletedNotification? TryMapHookCompleted(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetOptionalString(p, JsonFieldNames.TurnId, out var turnId) ||
            !AppServerNotificationParsing.TryParseHookRunSummaryInfo(p, JsonFieldNames.Run, out var runInfo, out var run))
        {
            return null;
        }

        return new HookCompletedNotification(threadId, turnId, run, runInfo, p);
    }

    private static CommandExecutionOutputDeltaNotification? TryMapCommandExecutionOutputDelta(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.TurnId, out var turnId) ||
            !TryGetRequiredString(p, JsonFieldNames.ItemId, out var itemId) ||
            !TryGetRequiredString(p, JsonFieldNames.Delta, out var delta))
        {
            return null;
        }

        return new CommandExecutionOutputDeltaNotification(threadId, turnId, itemId, delta, p);
    }

    private static RawResponseCompletedNotification? TryMapRawResponseCompleted(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.TurnId, out var turnId) ||
            !TryGetRequiredString(p, "responseId", out var responseId))
        {
            return null;
        }

        return new RawResponseCompletedNotification(
            threadId,
            turnId,
            responseId,
            GetOptionalAny(p, "usage"),
            GetOptionalAny(p, "usageMetadata"),
            p);
    }

    private static ThreadEnvironmentConnectionNotification? TryMapThreadEnvironmentConnection(string method, JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.EnvironmentId, out var environmentId))
        {
            return null;
        }

        return new ThreadEnvironmentConnectionNotification(method, threadId, environmentId, p);
    }

    private static ItemAutoApprovalReviewStartedNotification? TryMapAutoApprovalReviewStarted(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.TurnId, out var turnId) ||
            !TryGetRequiredString(p, JsonFieldNames.ReviewId, out var reviewId) ||
            !TryGetOptionalString(p, JsonFieldNames.TargetItemId, out var targetItemId) ||
            !AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(p, JsonFieldNames.Review, out var review))
        {
            return null;
        }

        return new ItemAutoApprovalReviewStartedNotification(
            threadId,
            turnId,
            reviewId,
            targetItemId,
            GetOptionalAny(p, JsonFieldNames.Action) ?? EmptyObject(),
            review,
            p);
    }

    private static ItemAutoApprovalReviewCompletedNotification? TryMapAutoApprovalReviewCompleted(JsonElement p)
    {
        if (!TryGetRequiredString(p, JsonFieldNames.ThreadId, out var threadId) ||
            !TryGetRequiredString(p, JsonFieldNames.TurnId, out var turnId) ||
            !TryGetRequiredString(p, JsonFieldNames.ReviewId, out var reviewId) ||
            !TryGetOptionalString(p, JsonFieldNames.TargetItemId, out var targetItemId) ||
            !TryGetRequiredString(p, "decisionSource", out var decisionSourceValue) ||
            !AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(p, JsonFieldNames.Review, out var review))
        {
            return null;
        }

        return new ItemAutoApprovalReviewCompletedNotification(
            threadId,
            turnId,
            reviewId,
            targetItemId,
            AppServerNotificationParsing.ParseAutoReviewDecisionSource(decisionSourceValue),
            GetOptionalAny(p, JsonFieldNames.Action) ?? EmptyObject(),
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
