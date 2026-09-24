using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadParsers
{
    public static IReadOnlyList<CodexThreadSummary> ParseThreadListThreads(JsonElement listResult)
    {
        var array =
            TryGetArray(listResult, JsonFieldNames.Data) ??
            TryGetArray(listResult, JsonFieldNames.Threads) ??
            TryGetArray(listResult, JsonFieldNames.Items) ??
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

        var primary = TryGetObject(threadObject, JsonFieldNames.Thread) ?? threadObject;
        var secondary = envelope is { ValueKind: JsonValueKind.Object } other ? other : threadObject;

        var threadId = ExtractThreadId(threadObject);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return null;
        }

        var name = GetString(primary, secondary, "name", "threadName", "title", "preview");
        var preview = GetString(primary, secondary, "preview");
        var projectId = GetString(primary, secondary, "projectId");

        var statusRaw = TryGetObject(primary, JsonFieldNames.Status) ?? TryGetObject(secondary, JsonFieldNames.Status);
        var statusType = statusRaw is { } st ? GetStringOrNull(st, JsonFieldNames.Type) : null;
        var activeFlags =
            string.Equals(statusType, "active", StringComparison.OrdinalIgnoreCase) &&
            statusRaw is { } sf
                ? GetOptionalStringArray(sf, JsonFieldNames.ActiveFlags)
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
        var isPinned = GetBool(primary, secondary, "isPinned");
        var section = ParseThreadSection(TryGetObject(primary, JsonFieldNames.Section) ?? TryGetObject(secondary, JsonFieldNames.Section));
        var sectionEnteredAt = GetUnixSecondsDateTimeOffset(primary, secondary, "sectionEnteredAt");
        var createdAt = GetDateTimeOffset(primary, secondary, "createdAt");
        var updatedAt = GetDateTimeOffset(primary, secondary, "updatedAt");
        var cwd = GetString(primary, secondary, "cwd");
        var pathValue = GetString(primary, secondary, "path");
        var model = GetString(primary, secondary, "model");
        var reasoningEffort = CodexReasoningEffort.TryParse(GetString(primary, secondary, "reasoningEffort"), out var parsedReasoningEffort)
            ? parsedReasoningEffort
            : (CodexReasoningEffort?)null;
        var modelProvider = GetString(primary, secondary, "modelProvider");
        var serviceTier = CodexServiceTier.TryParse(GetString(secondary, default, "serviceTier"), out var parsedServiceTier)
            ? parsedServiceTier
            : (CodexServiceTier?)null;
        var ephemeral = GetBool(primary, secondary, "ephemeral");
        var sourceKind = GetSourceKind(primary, secondary);
        var parentThreadId =
            GetString(primary, secondary, "parentThreadId") ??
            GetSourceParentThreadId(primary) ??
            GetSourceParentThreadId(secondary);
        var gitInfo = ParseGitInfo(primary, secondary);
        var cliVersion = GetString(primary, secondary, "cliVersion");
        var agentNickname = GetString(primary, secondary, "agentNickname");
        var agentRole = GetString(primary, secondary, "agentRole");
        var originator = GetString(primary, secondary, "originator");
        var turns = CodexAppServerClientThreadTurnParsers.ParseTurns(primary, secondary);
        var turnCount = turns?.Count ?? GetArrayCount(primary, secondary, "turns");

#pragma warning disable CS0618
        var summary = new CodexThreadSummary
        {
            ThreadId = threadId,
            Name = name,
            Archived = archived,
            IsPinned = isPinned,
            Section = section,
            SectionEnteredAt = sectionEnteredAt,
            StatusType = statusType,
            ActiveFlags = activeFlags,
            Preview = preview,
            ProjectId = projectId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Cwd = cwd,
            Path = pathValue,
            Model = model,
            ReasoningEffort = reasoningEffort,
            ModelProvider = modelProvider,
            ServiceTier = serviceTier,
            Ephemeral = ephemeral,
            SourceKind = sourceKind,
            ParentThreadId = parentThreadId,
            GitInfo = gitInfo,
            CliVersion = cliVersion,
            AgentNickname = agentNickname,
            AgentRole = agentRole,
            Originator = originator,
            TurnCount = turnCount,
            Turns = turns,
            Raw = threadObject,
            Status = status
        };
#pragma warning restore CS0618

        return summary;
    }

    public static string? ExtractNextCursor(JsonElement listResult) =>
        GetStringOrNull(listResult, JsonFieldNames.NextCursor) ??
        GetStringOrNull(listResult, JsonFieldNames.Cursor);

    public static ThreadSectionListPage ParseThreadSectionListPage(JsonElement result)
    {
        var array = TryGetArray(result, JsonFieldNames.Data)
            ?? throw new InvalidOperationException("threadSection/list response must contain a data array.");
        var sections = new List<ThreadSectionDescriptor>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("threadSection/list data[] entries must be objects.");
            }

            sections.Add(ParseRequiredThreadSection(item, "threadSection/list data[]"));
        }

        return new ThreadSectionListPage
        {
            Sections = sections,
            NextCursor = GetStringOrNull(result, JsonFieldNames.NextCursor),
            Raw = result
        };
    }

    public static ThreadSectionResult ParseThreadSectionResult(JsonElement result, string methodName)
    {
        var section = TryGetObject(result, JsonFieldNames.Section)
            ?? throw new InvalidOperationException($"{methodName} response must contain a section object.");

        return new ThreadSectionResult
        {
            Section = ParseRequiredThreadSection(section, $"{methodName} section"),
            Raw = result
        };
    }

    public static IReadOnlyList<string> ParseThreadLoadedListThreadIds(JsonElement loadedListResult)
    {
        var array =
            TryGetArray(loadedListResult, JsonFieldNames.Data) ??
            TryGetArray(loadedListResult, JsonFieldNames.Threads);

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

    private static DateTimeOffset? GetUnixSecondsDateTimeOffset(JsonElement primary, JsonElement secondary, string propertyName)
    {
        var value = GetInt64OrNull(primary, propertyName) ?? GetInt64OrNull(secondary, propertyName);
        return value is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
    }

    private static long? GetInt64OrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt64(out var i) => i,
            JsonValueKind.String when long.TryParse(p.GetString(), out var i) => i,
            _ => null
        };
    }

    private static ThreadSectionDescriptor? ParseThreadSection(JsonElement? section) =>
        section is { ValueKind: JsonValueKind.Object } value
            ? ParseRequiredThreadSection(value, "thread section")
            : null;

    private static ThreadSectionDescriptor ParseRequiredThreadSection(JsonElement section, string context) =>
        new()
        {
            Id = GetRequiredString(section, JsonFieldNames.Id, context),
            Name = GetRequiredString(section, JsonFieldNames.Name, context),
            Appearance = ParseThreadSectionAppearance(TryGetObject(section, JsonFieldNames.Appearance)),
            Raw = section.Clone()
        };

    private static ThreadSectionAppearance? ParseThreadSectionAppearance(JsonElement? appearance) =>
        appearance is { ValueKind: JsonValueKind.Object } value
            ? new ThreadSectionAppearance
            {
                Color = GetStringOrNull(value, JsonFieldNames.Color),
                Icon = GetStringOrNull(value, JsonFieldNames.Icon),
                Raw = value.Clone()
            }
            : null;

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
        var source = GetStringOrNull(primary, JsonFieldNames.Source) ?? GetStringOrNull(secondary, JsonFieldNames.Source);
        if (!string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        var sourceObject = TryGetObject(primary, JsonFieldNames.Source) ?? TryGetObject(secondary, JsonFieldNames.Source);
        if (sourceObject is not { } so)
        {
            return null;
        }

        var kind = GetStringOrNull(so, JsonFieldNames.Kind) ?? GetStringOrNull(so, JsonFieldNames.Type);
        if (!string.IsNullOrWhiteSpace(kind))
        {
            return kind;
        }

        if (TryGetObject(so, JsonFieldNames.SubAgent) is { } subAgent)
        {
            if (TryGetObject(subAgent, JsonFieldNames.Review) is not null)
            {
                return "subAgentReview";
            }

            if (TryGetObject(subAgent, "compact") is not null)
            {
                return "subAgentCompact";
            }

            if (TryGetObject(subAgent, JsonFieldNames.ThreadSpawn) is not null ||
                TryGetObject(subAgent, JsonFieldNames.SnakeCase.ThreadSpawn) is not null)
            {
                return "subAgentThreadSpawn";
            }

            return "subAgentOther";
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

    private static string? GetSourceParentThreadId(JsonElement sourceOwner)
    {
        if (TryGetObject(sourceOwner, JsonFieldNames.Source) is not { } source ||
            TryGetObject(source, JsonFieldNames.SubAgent) is not { } subAgent)
        {
            return null;
        }

        var threadSpawn =
            TryGetObject(subAgent, JsonFieldNames.ThreadSpawn) ??
            TryGetObject(subAgent, JsonFieldNames.SnakeCase.ThreadSpawn);

        return threadSpawn is { } spawn
            ? GetStringOrNull(spawn, "parentThreadId") ?? GetStringOrNull(spawn, "parent_thread_id")
            : null;
    }

    private static CodexThreadGitInfo? ParseGitInfo(JsonElement primary, JsonElement secondary)
    {
        var gitInfo = TryGetObject(primary, JsonFieldNames.GitInfo) ?? TryGetObject(secondary, JsonFieldNames.GitInfo);
        if (gitInfo is not { } raw)
        {
            return null;
        }

        return new CodexThreadGitInfo
        {
            Sha = GetStringOrNull(raw, JsonFieldNames.Sha),
            Branch = GetStringOrNull(raw, JsonFieldNames.Branch),
            OriginUrl = GetStringOrNull(raw, JsonFieldNames.OriginUrl),
            Raw = raw.Clone()
        };
    }
}
