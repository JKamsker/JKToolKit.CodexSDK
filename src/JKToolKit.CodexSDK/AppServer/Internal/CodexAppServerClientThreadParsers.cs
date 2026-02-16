using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadParsers
{
    public static IReadOnlyList<CodexThreadSummary> ParseThreadListThreads(JsonElement listResult)
    {
        var array =
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
            GetStringOrNull(primary, "title");

        var archived = GetBoolOrNull(primary, "archived");
        var createdAt = GetDateTimeOffsetOrNull(primary, "createdAt");
        var cwd = GetStringOrNull(primary, "cwd");
        var model = GetStringOrNull(primary, "model");

        return new CodexThreadSummary
        {
            ThreadId = threadId,
            Name = name,
            Archived = archived,
            CreatedAt = createdAt,
            Cwd = cwd,
            Model = model,
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

