using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Summary of available account rate-limit reset credits.
/// </summary>
public sealed record class RateLimitResetCreditsSummary
{
    /// <summary>
    /// Gets the available reset-credit count.
    /// </summary>
    public long AvailableCount { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the summary.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>account/rateLimitResetCredit/consume</c>.
/// </summary>
public sealed class AccountRateLimitResetCreditConsumeOptions
{
    /// <summary>
    /// Gets or sets a stable idempotency key for this reset attempt.
    /// </summary>
    public required string IdempotencyKey { get; set; }
}

/// <summary>
/// Result returned by <c>account/rateLimitResetCredit/consume</c>.
/// </summary>
public sealed record class AccountRateLimitResetCreditConsumeResult
{
    /// <summary>
    /// Gets the upstream outcome value.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
