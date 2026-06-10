using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Result returned by <c>account/usage/read</c>.
/// </summary>
public sealed record class AccountTokenUsageReadResult
{
    /// <summary>
    /// Gets account token-usage summary counters.
    /// </summary>
    public required AccountTokenUsageSummary Summary { get; init; }

    /// <summary>
    /// Gets daily token-usage buckets, when upstream returns them.
    /// </summary>
    public IReadOnlyList<AccountTokenUsageDailyBucket>? DailyUsageBuckets { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Aggregated account token-usage counters.
/// </summary>
public sealed record class AccountTokenUsageSummary
{
    /// <summary>
    /// Gets lifetime token usage, when available.
    /// </summary>
    public long? LifetimeTokens { get; init; }

    /// <summary>
    /// Gets peak daily token usage, when available.
    /// </summary>
    public long? PeakDailyTokens { get; init; }

    /// <summary>
    /// Gets the longest running turn duration in seconds, when available.
    /// </summary>
    public long? LongestRunningTurnSec { get; init; }

    /// <summary>
    /// Gets the current daily-usage streak length, when available.
    /// </summary>
    public long? CurrentStreakDays { get; init; }

    /// <summary>
    /// Gets the longest daily-usage streak length, when available.
    /// </summary>
    public long? LongestStreakDays { get; init; }
}

/// <summary>
/// Daily account token-usage bucket.
/// </summary>
public sealed record class AccountTokenUsageDailyBucket
{
    /// <summary>
    /// Gets the bucket start date string.
    /// </summary>
    public required string StartDate { get; init; }

    /// <summary>
    /// Gets token usage for the bucket.
    /// </summary>
    public long Tokens { get; init; }
}
