namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for reading account rate-limit state via <c>account/rateLimits/read</c>.
/// </summary>
public sealed class AccountRateLimitsReadOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the client supports automatic Luna Reserve fallback.
    /// </summary>
    public bool? SupportsLunaReserve { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the backend should skip reset-credit detail lookups.
    /// </summary>
    public bool? ExcludeResetCreditDetails { get; set; }
}
