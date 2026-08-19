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
    /// Gets estimated usage for the requested thread, when upstream can resolve a thread billing route.
    /// </summary>
    public AccountThreadUsage? ThreadUsage { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>account/usage/read</c>.
/// </summary>
public sealed class AccountTokenUsageReadOptions
{
    /// <summary>
    /// Gets or sets an optional loaded thread id used to read thread-specific estimated usage.
    /// </summary>
    public string? ThreadId { get; set; }
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

/// <summary>
/// Estimated thread usage returned by <c>account/usage/read</c>.
/// </summary>
public sealed record class AccountThreadUsage
{
    /// <summary>
    /// Gets the thread identifier this usage describes.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets estimated usage in account credits, in micros.
    /// </summary>
    public long? EstimatedUsageCreditsMicros { get; init; }

    /// <summary>
    /// Gets estimated usage in USD, in micros, when available.
    /// </summary>
    public long? EstimatedUsageUsdMicros { get; init; }

    /// <summary>
    /// Gets usage breakdown groups returned by upstream.
    /// </summary>
    public required IReadOnlyList<AccountThreadUsageBreakdownGroup> Groups { get; init; }

    /// <summary>
    /// Gets the raw thread usage payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// One model/speed usage breakdown group for a thread.
/// </summary>
public sealed record class AccountThreadUsageBreakdownGroup
{
    /// <summary>
    /// Gets the model id for this usage group.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the reasoning-effort value for this usage group.
    /// </summary>
    public string? ReasoningEffort { get; init; }

    /// <summary>
    /// Gets the service speed/tier value for this usage group.
    /// </summary>
    public string? Speed { get; init; }

    /// <summary>
    /// Gets input tokens counted for this usage group.
    /// </summary>
    public long? InputTokens { get; init; }

    /// <summary>
    /// Gets cached input tokens counted for this usage group.
    /// </summary>
    public long? CachedInputTokens { get; init; }

    /// <summary>
    /// Gets net-new input tokens counted for this usage group.
    /// </summary>
    public long? NetNewInputTokens { get; init; }

    /// <summary>
    /// Gets output tokens counted for this usage group.
    /// </summary>
    public long? OutputTokens { get; init; }

    /// <summary>
    /// Gets total tokens counted for this usage group.
    /// </summary>
    public long? TotalTokens { get; init; }

    /// <summary>
    /// Gets estimated usage in account credits, in micros.
    /// </summary>
    public long? EstimatedUsageCreditsMicros { get; init; }

    /// <summary>
    /// Gets the raw usage group payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
