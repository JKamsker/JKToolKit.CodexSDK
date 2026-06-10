using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerConfigClient
{
    public async Task<AccountTokenUsageReadResult> ReadAccountTokenUsageAsync(CancellationToken ct = default)
    {
        var result = await _sendRequestAsync(
            "account/usage/read",
            null,
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
}
