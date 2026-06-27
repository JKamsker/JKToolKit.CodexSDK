using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>account/rateLimitResetCredit/consume</c>.
/// </summary>
public sealed class AccountRateLimitResetCreditConsumeOptions
{
    /// <summary>
    /// Gets or sets the optional reset-credit type requested by the caller.
    /// </summary>
    public string? CreditType { get; set; }
}

/// <summary>
/// Represents the result returned by <c>account/rateLimitResetCredit/consume</c>.
/// </summary>
public sealed record class AccountRateLimitResetCreditConsumeResult
{
    /// <summary>
    /// Gets the upstream outcome discriminator, when present.
    /// </summary>
    public string? Outcome { get; init; }

    /// <summary>
    /// Gets the optional rate-limit reset credits summary.
    /// </summary>
    public JsonElement? RateLimitResetCredits { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
