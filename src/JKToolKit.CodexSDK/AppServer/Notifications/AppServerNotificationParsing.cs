using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static class AppServerNotificationParsing
{
    public static IReadOnlyList<AppDescriptor> ParseAppsList(JsonElement obj) =>
        Internal.CodexAppServerClientSkillsAppsParsers.ParseAppsListApps(obj);

    public static IReadOnlyList<FuzzyFileSearchResult> ParseFuzzyFileSearchResults(JsonElement obj)
    {
        if (!TryGetArray(obj, "files", out var files))
        {
            return Array.Empty<FuzzyFileSearchResult>();
        }

        var results = new List<FuzzyFileSearchResult>();
        foreach (var item in files.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var matchType = GetString(item, "matchType") ?? GetString(item, "match_type");
            results.Add(new FuzzyFileSearchResult
            {
                Root = GetString(item, "root") ?? string.Empty,
                Path = GetString(item, "path") ?? string.Empty,
                FileName = GetString(item, "fileName") ?? GetString(item, "file_name") ?? string.Empty,
                Score = GetUInt32(item, "score"),
                MatchType = matchType,
                MatchKind = ParseMatchType(matchType),
                Indices = ParseIndices(item)
            });
        }

        return results;
    }

    public static GuardianApprovalReviewInfo ParseGuardianApprovalReviewInfo(JsonElement obj, string propertyName)
    {
        var review = TryGetObject(obj, propertyName, out var parsedReview)
            ? parsedReview
            : EmptyObject();
        var statusValue = GetString(review, "status");

        return new GuardianApprovalReviewInfo
        {
            Status = ParseGuardianApprovalReviewStatus(statusValue),
            StatusValue = statusValue,
            Rationale = GetString(review, "rationale"),
            RiskScore = GetInt32OrNull(review, "riskScore"),
            Raw = review
        };
    }

    public static JsonElement GetAny(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var property)
            ? property.Clone()
            : EmptyObject();

    public static string? GetScalarText(JsonElement obj, string propertyName)
    {
        if (!TryGetProperty(obj, propertyName, out var property))
        {
            return null;
        }

        return GetScalarText(property);
    }

    public static string? GetScalarText(JsonElement property) =>
        property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };

    private static FuzzyFileSearchMatchType ParseMatchType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FuzzyFileSearchMatchType.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "file" => FuzzyFileSearchMatchType.File,
            "directory" => FuzzyFileSearchMatchType.Directory,
            "dir" => FuzzyFileSearchMatchType.Directory,
            "path" => FuzzyFileSearchMatchType.File,
            "filename" => FuzzyFileSearchMatchType.File,
            "file_name" => FuzzyFileSearchMatchType.File,
            _ => FuzzyFileSearchMatchType.Unknown
        };
    }

    private static GuardianApprovalReviewStatus ParseGuardianApprovalReviewStatus(string? value) =>
        value?.Trim() switch
        {
            "inProgress" => GuardianApprovalReviewStatus.InProgress,
            "approved" => GuardianApprovalReviewStatus.Approved,
            "denied" => GuardianApprovalReviewStatus.Denied,
            "aborted" => GuardianApprovalReviewStatus.Aborted,
            _ => GuardianApprovalReviewStatus.Unknown
        };

    private static IReadOnlyList<uint>? ParseIndices(JsonElement item)
    {
        if (!TryGetArray(item, "indices", out var indices))
        {
            return null;
        }

        var parsedIndices = new List<uint>();
        foreach (var index in indices.EnumerateArray())
        {
            if (index.ValueKind == JsonValueKind.Number && index.TryGetUInt32(out var value))
            {
                parsedIndices.Add(value);
            }
        }

        return parsedIndices;
    }

    private static uint GetUInt32(JsonElement obj, string propertyName)
    {
        if (!TryGetProperty(obj, propertyName, out var property))
        {
            return default;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && uint.TryParse(property.GetString(), out number))
        {
            return number;
        }

        return default;
    }

    private static int? GetInt32OrNull(JsonElement obj, string propertyName)
    {
        if (!TryGetProperty(obj, propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string propertyName) =>
        TryGetProperty(obj, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetProperty(JsonElement obj, string propertyName, out JsonElement property)
    {
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static bool TryGetObject(JsonElement obj, string propertyName, out JsonElement property)
    {
        if (TryGetProperty(obj, propertyName, out property) && property.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        property = default;
        return false;
    }

    private static bool TryGetArray(JsonElement obj, string propertyName, out JsonElement property)
    {
        if (TryGetProperty(obj, propertyName, out property) && property.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        property = default;
        return false;
    }

    private static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}
