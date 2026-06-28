using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Summarizes earned account rate-limit reset credits.
/// </summary>
public sealed record class RateLimitResetCreditsSummary
{
    /// <summary>
    /// Gets the number of reset credits currently available.
    /// </summary>
    public long AvailableCount { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the reset-credit summary.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the result returned by <c>account/rateLimitResetCredit/consume</c>.
/// </summary>
public sealed record class AccountRateLimitResetCreditConsumeResult
{
    /// <summary>
    /// Gets the raw upstream outcome string.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the parsed reset-credit consume outcome.
    /// </summary>
    public AccountRateLimitResetCreditConsumeOutcome OutcomeKind { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Known <c>account/rateLimitResetCredit/consume</c> outcomes.
/// </summary>
public enum AccountRateLimitResetCreditConsumeOutcome
{
    /// <summary>
    /// The server returned an outcome unknown to this SDK version.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A reset credit was consumed and eligible rate-limit windows were reset.
    /// </summary>
    Reset,

    /// <summary>
    /// No current rate-limit window was eligible for a reset.
    /// </summary>
    NothingToReset,

    /// <summary>
    /// No earned reset credits were available.
    /// </summary>
    NoCredit,

    /// <summary>
    /// The same idempotency key had already completed a reset.
    /// </summary>
    AlreadyRedeemed
}
