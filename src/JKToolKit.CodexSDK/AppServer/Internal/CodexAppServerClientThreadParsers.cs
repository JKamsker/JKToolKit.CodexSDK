using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadParsers
{
    public static IReadOnlyList<CodexThreadSummary> ParseThreadListThreads(JsonElement listResult)
    {
        var array =
            TryGetArray(listResult, "data") ??
            TryGetArray(listResult, "threads") ??
            TryGetArray(listResult, "items") ??
            TryGetArray(listResult, "sessions");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CodexThreadSummary>();
        }

        var threads = new List<CodexThreadSummary>();
        foreach (var item in array.Value.EnumerateArray())
        {
            var summary = ParseThreadSummary(item);
            if (summary is not null)
            {
                threads.Add(summary);
            }
        }

        return threads;
    }

    public static CodexThreadSummary? ParseThreadSummary(JsonElement threadObject)
    {
        if (threadObject.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var primary = TryGetObject(threadObject, "thread") ?? threadObject;

        var threadId = ExtractThreadId(threadObject);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return null;
        }

        var name =
            GetStringOrNull(primary, "name") ??
            GetStringOrNull(primary, "threadName") ??
            GetStringOrNull(primary, "title") ??
            GetStringOrNull(primary, "preview");

        var status = TryGetObject(primary, "status");
        var statusType = status is { } st ? GetStringOrNull(st, "type") : null;
        var activeFlags =
            string.Equals(statusType, "active", StringComparison.OrdinalIgnoreCase) &&
            status is { } sf
                ? GetOptionalStringArray(sf, "activeFlags")
                : null;

        var archived = GetBoolOrNull(primary, "archived");
        if (archived is null &&
            GetStringOrNull(primary, "path") is { Length: > 0 } path &&
            path.Contains("archived_sessions", StringComparison.OrdinalIgnoreCase))
        {
            archived = true;
        }
        var createdAt = GetDateTimeOffsetOrNull(primary, "createdAt");
        var cwd = GetStringOrNull(primary, "cwd");
        var model =
            GetStringOrNull(primary, "model") ??
            GetStringOrNull(primary, "modelProvider");
        var serviceTier = CodexServiceTier.TryParse(GetStringOrNull(primary, "serviceTier"), out var parsedServiceTier)
            ? parsedServiceTier
            : (CodexServiceTier?)null;

        return new CodexThreadSummary
        {
            ThreadId = threadId,
            Name = name,
            Archived = archived,
            StatusType = statusType,
            ActiveFlags = activeFlags,
            CreatedAt = createdAt,
            Cwd = cwd,
            Model = model,
            ServiceTier = serviceTier,
            Raw = threadObject
        };
    }

    public static string? ExtractNextCursor(JsonElement listResult) =>
        GetStringOrNull(listResult, "nextCursor") ??
        GetStringOrNull(listResult, "cursor");

    public static IReadOnlyList<string> ParseThreadLoadedListThreadIds(JsonElement loadedListResult)
    {
        var array =
            TryGetArray(loadedListResult, "data") ??
            TryGetArray(loadedListResult, "threads");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var ids = new List<string>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var id = item.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}

