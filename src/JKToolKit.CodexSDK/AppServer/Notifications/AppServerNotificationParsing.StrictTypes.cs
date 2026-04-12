using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationParsing
{
    public static bool TryParseGuardianApprovalReviewInfo(JsonElement obj, string propertyName, out GuardianApprovalReviewInfo review)
    {
        review = default!;
        if (!TryGetObject(obj, propertyName, out var parsedReview))
        {
            return false;
        }

        var statusValue = GetString(parsedReview, "status");
        if (string.IsNullOrWhiteSpace(statusValue))
        {
            return false;
        }

        if (!TryGetOptionalString(parsedReview, "rationale", out var rationale) ||
            !TryGetOptionalInt32(parsedReview, "riskScore", out var riskScore) ||
            !TryGetOptionalString(parsedReview, "riskLevel", out var riskLevelValue) ||
            !TryGetOptionalString(parsedReview, "userAuthorization", out var userAuthorizationValue))
        {
            return false;
        }

        review = new GuardianApprovalReviewInfo
        {
            Status = ParseGuardianApprovalReviewStatus(statusValue),
            StatusValue = statusValue,
            Rationale = rationale,
            RiskScore = riskScore,
            RiskLevel = ParseGuardianRiskLevel(riskLevelValue),
            UserAuthorization = ParseGuardianUserAuthorization(userAuthorizationValue),
            Raw = parsedReview.Clone()
        };
        return true;
    }

    public static bool TryParseHookRunSummaryInfo(JsonElement obj, string propertyName, out HookRunSummaryInfo runInfo, out JsonElement runRaw)
    {
        runInfo = default!;
        runRaw = default;
        if (!TryGetObject(obj, propertyName, out var run))
        {
            return false;
        }

        if (!TryGetRequiredString(run, "id", out var id) ||
            !TryGetRequiredString(run, "eventName", out var eventName) ||
            !TryGetRequiredString(run, "handlerType", out var handlerType) ||
            !TryGetRequiredString(run, "executionMode", out var executionMode) ||
            !TryGetRequiredString(run, "scope", out var scope) ||
            !TryGetRequiredString(run, "sourcePath", out var sourcePath) ||
            !TryGetRequiredInt64(run, "displayOrder", out var displayOrder) ||
            !TryGetRequiredString(run, "status", out var status) ||
            !TryGetRequiredInt64(run, "startedAt", out var startedAt) ||
            !TryGetOptionalString(run, "statusMessage", out var statusMessage) ||
            !TryGetOptionalInt64(run, "completedAt", out var completedAt) ||
            !TryGetOptionalInt64(run, "durationMs", out var durationMs) ||
            !TryParseHookOutputEntries(run, out var entries))
        {
            return false;
        }

        runRaw = run.Clone();
        runInfo = new HookRunSummaryInfo
        {
            Id = id,
            EventName = eventName,
            HandlerType = handlerType,
            ExecutionMode = executionMode,
            Scope = scope,
            SourcePath = sourcePath,
            DisplayOrder = displayOrder,
            Status = status,
            StatusMessage = statusMessage,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = durationMs,
            Entries = entries,
            Raw = runRaw
        };
        return true;
    }

    private static GuardianRiskLevel? ParseGuardianRiskLevel(string? value) =>
        value?.Trim() switch
        {
            "low" => GuardianRiskLevel.Low,
            "medium" => GuardianRiskLevel.Medium,
            "high" => GuardianRiskLevel.High,
            "critical" => GuardianRiskLevel.Critical,
            null => null,
            _ => GuardianRiskLevel.Unknown
        };

    private static bool TryParseHookOutputEntries(JsonElement run, out IReadOnlyList<HookOutputEntryInfo> entries)
    {
        entries = Array.Empty<HookOutputEntryInfo>();
        if (!TryGetArray(run, "entries", out var entriesArray))
        {
            return false;
        }

        var parsed = new List<HookOutputEntryInfo>();
        foreach (var entry in entriesArray.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !TryGetRequiredString(entry, "kind", out var kind) ||
                !TryGetRequiredString(entry, "text", out var text))
            {
                return false;
            }

            parsed.Add(new HookOutputEntryInfo
            {
                Kind = kind,
                Text = text,
                Raw = entry.Clone()
            });
        }

        entries = parsed;
        return true;
    }

    private static bool TryGetRequiredString(JsonElement obj, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(obj, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetRequiredInt64(JsonElement obj, string propertyName, out long value)
    {
        value = default;
        return TryGetProperty(obj, propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out value);
    }

    private static bool TryGetOptionalInt64(JsonElement obj, string propertyName, out long? value)
    {
        value = null;
        if (!TryGetProperty(obj, propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static bool TryGetOptionalInt32(JsonElement obj, string propertyName, out int? value)
    {
        value = null;
        if (!TryGetProperty(obj, propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static bool TryGetOptionalString(JsonElement obj, string propertyName, out string? value)
    {
        value = null;
        if (!TryGetProperty(obj, propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }
}
