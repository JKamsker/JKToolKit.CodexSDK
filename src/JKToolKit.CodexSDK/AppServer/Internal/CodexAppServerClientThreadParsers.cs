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
            var summary = ParseThreadSummary(item, item);
            if (summary is not null)
            {
                threads.Add(summary);
            }
        }

        return threads;
    }

    public static CodexThreadSummary? ParseThreadSummary(JsonElement threadObject, JsonElement? envelope = null)
    {
        if (threadObject.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var primary = TryGetObject(threadObject, "thread") ?? threadObject;
        var secondary = envelope is { ValueKind: JsonValueKind.Object } other ? other : threadObject;

        var threadId = ExtractThreadId(threadObject);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return null;
        }

        var name = GetString(primary, secondary, "name", "threadName", "title", "preview");
        var preview = GetString(primary, secondary, "preview");

        var statusRaw = TryGetObject(primary, "status") ?? TryGetObject(secondary, "status");
        var statusType = statusRaw is { } st ? GetStringOrNull(st, "type") : null;
        var activeFlags =
            string.Equals(statusType, "active", StringComparison.OrdinalIgnoreCase) &&
            statusRaw is { } sf
                ? GetOptionalStringArray(sf, "activeFlags")
                : null;
        var status = statusType is { } type && statusRaw is { } raw
            ? new CodexThreadStatus(type, activeFlags, raw.Clone())
            : null;

        var archived = GetBool(primary, secondary, "archived");
        if (archived is null &&
            GetString(primary, secondary, "path") is { Length: > 0 } path &&
            path.Contains("archived_sessions", StringComparison.OrdinalIgnoreCase))
        {
            archived = true;
        }
        var createdAt = GetDateTimeOffset(primary, secondary, "createdAt");
        var updatedAt = GetDateTimeOffset(primary, secondary, "updatedAt");
        var cwd = GetString(primary, secondary, "cwd");
        var pathValue = GetString(primary, secondary, "path");
        var model = GetString(secondary, default, "model");
        var modelProvider = GetString(primary, secondary, "modelProvider");
        var serviceTier = CodexServiceTier.TryParse(GetString(secondary, default, "serviceTier"), out var parsedServiceTier)
            ? parsedServiceTier
            : (CodexServiceTier?)null;
        var ephemeral = GetBool(primary, secondary, "ephemeral");
        var sourceKind = GetSourceKind(primary, secondary);
        var cliVersion = GetString(primary, secondary, "cliVersion");
        var agentNickname = GetString(primary, secondary, "agentNickname");
        var agentRole = GetString(primary, secondary, "agentRole");
        var turnCount = GetArrayCount(primary, secondary, "turns");

        return new CodexThreadSummary
        {
            ThreadId = threadId,
            Name = name,
            Archived = archived,
            StatusType = statusType,
            ActiveFlags = activeFlags,
            Preview = preview,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Cwd = cwd,
            Path = pathValue,
            Model = model,
            ModelProvider = modelProvider,
            ServiceTier = serviceTier,
            Ephemeral = ephemeral,
            SourceKind = sourceKind,
            CliVersion = cliVersion,
            AgentNickname = agentNickname,
            AgentRole = agentRole,
            TurnCount = turnCount,
            Raw = threadObject,
            Status = status
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

                continue;
            }

            var threadId = ExtractThreadId(item);
            if (!string.IsNullOrWhiteSpace(threadId))
            {
                ids.Add(threadId);
            }
        }

        return ids;
    }

    private static string? GetString(JsonElement primary, JsonElement secondary, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetStringOrNull(primary, propertyName) ?? GetStringOrNull(secondary, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement primary, JsonElement secondary, string propertyName) =>
        GetBoolOrNull(primary, propertyName) ?? GetBoolOrNull(secondary, propertyName);

    private static DateTimeOffset? GetDateTimeOffset(JsonElement primary, JsonElement secondary, string propertyName) =>
        GetDateTimeOffsetOrNull(primary, propertyName) ?? GetDateTimeOffsetOrNull(secondary, propertyName);

    private static int? GetArrayCount(JsonElement primary, JsonElement secondary, string propertyName)
    {
        var array = TryGetArray(primary, propertyName) ?? TryGetArray(secondary, propertyName);
        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return array.Value.GetArrayLength();
    }

    private static string? GetSourceKind(JsonElement primary, JsonElement secondary)
    {
        var source = GetStringOrNull(primary, "source") ?? GetStringOrNull(secondary, "source");
        if (!string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        var sourceObject = TryGetObject(primary, "source") ?? TryGetObject(secondary, "source");
        if (sourceObject is not { } so)
        {
            return null;
        }

        var kind = GetStringOrNull(so, "kind") ?? GetStringOrNull(so, "type");
        if (!string.IsNullOrWhiteSpace(kind))
        {
            return kind;
        }

        foreach (var property in so.EnumerateObject())
        {
            var name = property.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.ToLowerInvariant();
            }
        }

        return null;
    }
}

