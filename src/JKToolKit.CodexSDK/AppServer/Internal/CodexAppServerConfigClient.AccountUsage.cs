using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerConfigClient
{
    public Task<AccountTokenUsageReadResult> ReadAccountTokenUsageAsync(CancellationToken ct = default) =>
        ReadAccountTokenUsageAsync(new AccountTokenUsageReadOptions(), ct);

    public async Task<AccountTokenUsageReadResult> ReadAccountTokenUsageAsync(
        AccountTokenUsageReadOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "account/usage/read",
            string.IsNullOrWhiteSpace(options.ThreadId) ? null : new { options.ThreadId },
            ct);

        return ParseAccountTokenUsage(result);
    }

    private static AccountTokenUsageReadResult ParseAccountTokenUsage(JsonElement result)
    {
        var summary = CodexAppServerClientJson.TryGetObject(result, "summary")
            ?? throw new InvalidOperationException("account/usage/read response missing required object property 'summary'.");

        return new AccountTokenUsageReadResult
        {
            Summary = new AccountTokenUsageSummary
            {
                LifetimeTokens = CodexAppServerClientJson.GetInt64OrNull(summary, "lifetimeTokens"),
                PeakDailyTokens = CodexAppServerClientJson.GetInt64OrNull(summary, "peakDailyTokens"),
                LongestRunningTurnSec = CodexAppServerClientJson.GetInt64OrNull(summary, "longestRunningTurnSec"),
                CurrentStreakDays = CodexAppServerClientJson.GetInt64OrNull(summary, "currentStreakDays"),
                LongestStreakDays = CodexAppServerClientJson.GetInt64OrNull(summary, "longestStreakDays")
            },
            DailyUsageBuckets = ParseDailyUsageBuckets(result),
            ThreadUsage = ParseThreadUsage(result),
            Raw = result
        };
    }

    private static IReadOnlyList<AccountTokenUsageDailyBucket>? ParseDailyUsageBuckets(JsonElement result)
    {
        var bucketsArray = CodexAppServerClientJson.TryGetArray(result, "dailyUsageBuckets");
        if (bucketsArray is null)
        {
            return null;
        }

        var buckets = new List<AccountTokenUsageDailyBucket>();
        foreach (var item in bucketsArray.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("account/usage/read dailyUsageBuckets[] entries must be objects.");
            }

            buckets.Add(new AccountTokenUsageDailyBucket
            {
                StartDate = CodexAppServerClientJson.GetRequiredString(item, "startDate", "account/usage/read dailyUsageBuckets[]"),
                Tokens = CodexAppServerClientJson.GetRequiredInt64(item, "tokens", "account/usage/read dailyUsageBuckets[]")
            });
        }

        return buckets;
    }

    private static AccountThreadUsage? ParseThreadUsage(JsonElement result)
    {
        if (CodexAppServerClientJson.TryGetObject(result, "threadUsage") is not { } usage)
        {
            return null;
        }

        return new AccountThreadUsage
        {
            ThreadId = CodexAppServerClientJson.GetStringOrNull(usage, "threadId"),
            EstimatedUsageCreditsMicros = CodexAppServerClientJson.GetInt64OrNull(usage, "estimatedUsageCreditsMicros"),
            EstimatedUsageUsdMicros = CodexAppServerClientJson.GetInt64OrNull(usage, "estimatedUsageUsdMicros"),
            Groups = ParseThreadUsageGroups(usage),
            Raw = usage.Clone()
        };
    }

    private static IReadOnlyList<AccountThreadUsageBreakdownGroup> ParseThreadUsageGroups(JsonElement usage)
    {
        var groupsArray = CodexAppServerClientJson.TryGetArray(usage, "groups");
        if (groupsArray is null)
        {
            return Array.Empty<AccountThreadUsageBreakdownGroup>();
        }

        var groups = new List<AccountThreadUsageBreakdownGroup>();
        foreach (var item in groupsArray.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            groups.Add(new AccountThreadUsageBreakdownGroup
            {
                Model = CodexAppServerClientJson.GetStringOrNull(item, "model"),
                ReasoningEffort = CodexAppServerClientJson.GetStringOrNull(item, "reasoningEffort"),
                Speed = CodexAppServerClientJson.GetStringOrNull(item, "speed"),
                InputTokens = CodexAppServerClientJson.GetInt64OrNull(item, "inputTokens"),
                CachedInputTokens = CodexAppServerClientJson.GetInt64OrNull(item, "cachedInputTokens"),
                NetNewInputTokens = CodexAppServerClientJson.GetInt64OrNull(item, "netNewInputTokens"),
                OutputTokens = CodexAppServerClientJson.GetInt64OrNull(item, "outputTokens"),
                TotalTokens = CodexAppServerClientJson.GetInt64OrNull(item, "totalTokens"),
                EstimatedUsageCreditsMicros = CodexAppServerClientJson.GetInt64OrNull(item, "estimatedUsageCreditsMicros"),
                Raw = item.Clone()
            });
        }

        return groups;
    }
}
