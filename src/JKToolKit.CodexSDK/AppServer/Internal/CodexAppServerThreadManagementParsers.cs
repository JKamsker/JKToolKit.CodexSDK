using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerThreadManagementParsers
{
    public static PermissionProfileListPage ParsePermissionProfiles(JsonElement result)
    {
        var profiles = new List<PermissionProfileSummary>();
        if (TryGetArray(result, JsonFieldNames.Data) is { } data)
        {
            foreach (var item in data.EnumerateArray())
            {
                var id = GetStringOrNull(item, JsonFieldNames.Id);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                profiles.Add(new PermissionProfileSummary
                {
                    Id = id,
                    Description = GetStringOrNull(item, JsonFieldNames.Description),
                    Raw = item.Clone()
                });
            }
        }

        return new PermissionProfileListPage
        {
            Profiles = profiles,
            NextCursor = GetStringOrNull(result, JsonFieldNames.NextCursor),
            Raw = result
        };
    }

    public static ThreadSearchPage ParseThreadSearch(JsonElement result)
    {
        var results = new List<ThreadSearchResult>();
        if (TryGetArray(result, JsonFieldNames.Data) is { } data)
        {
            foreach (var item in data.EnumerateArray())
            {
                var thread = CodexAppServerClientThreadParsers.ParseThreadSummary(item, item);
                if (thread is null)
                {
                    continue;
                }

                results.Add(new ThreadSearchResult
                {
                    Thread = thread,
                    Snippet = GetStringOrNull(item, "snippet") ?? string.Empty,
                    Raw = item.Clone()
                });
            }
        }

        return new ThreadSearchPage
        {
            Results = results,
            NextCursor = GetStringOrNull(result, JsonFieldNames.NextCursor),
            BackwardsCursor = GetStringOrNull(result, JsonFieldNames.BackwardsCursor),
            Raw = result
        };
    }

    public static ThreadGoalResult ParseThreadGoalResult(JsonElement result)
    {
        var goal = TryGetObject(result, JsonFieldNames.Goal) is { } raw ? ParseThreadGoal(raw) : null;
        return new ThreadGoalResult { Goal = goal, Raw = result };
    }

    public static ThreadGoal? ParseThreadGoal(JsonElement goal)
    {
        if (goal.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var threadId = GetStringOrNull(goal, JsonFieldNames.ThreadId);
        var objective = GetStringOrNull(goal, "objective");
        var statusValue = GetStringOrNull(goal, JsonFieldNames.Status);
        if (string.IsNullOrWhiteSpace(threadId) ||
            objective is null ||
            string.IsNullOrWhiteSpace(statusValue))
        {
            return null;
        }

        return new ThreadGoal
        {
            ThreadId = threadId,
            Objective = objective,
            StatusValue = statusValue,
            Status = ParseThreadGoalStatus(statusValue),
            TokenBudget = GetInt64OrNull(goal, "tokenBudget"),
            TokensUsed = GetInt64OrNull(goal, "tokensUsed") ?? 0,
            TimeUsedSeconds = GetInt64OrNull(goal, "timeUsedSeconds") ?? 0,
            CreatedAt = GetInt64OrNull(goal, JsonFieldNames.CreatedAt) ?? 0,
            UpdatedAt = GetInt64OrNull(goal, JsonFieldNames.UpdatedAt) ?? 0,
            Raw = goal.Clone()
        };
    }

    public static ThreadGoalStatus ParseThreadGoalStatus(string value) =>
        value switch
        {
            "active" => ThreadGoalStatus.Active,
            "paused" => ThreadGoalStatus.Paused,
            "blocked" => ThreadGoalStatus.Blocked,
            "usageLimited" => ThreadGoalStatus.UsageLimited,
            "budgetLimited" => ThreadGoalStatus.BudgetLimited,
            "complete" => ThreadGoalStatus.Complete,
            _ => ThreadGoalStatus.Unknown
        };

    public static string FormatThreadGoalStatus(ThreadGoalStatus status) =>
        status switch
        {
            ThreadGoalStatus.Active => "active",
            ThreadGoalStatus.Paused => "paused",
            ThreadGoalStatus.Blocked => "blocked",
            ThreadGoalStatus.UsageLimited => "usageLimited",
            ThreadGoalStatus.BudgetLimited => "budgetLimited",
            ThreadGoalStatus.Complete => "complete",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported thread goal status.")
        };
}
