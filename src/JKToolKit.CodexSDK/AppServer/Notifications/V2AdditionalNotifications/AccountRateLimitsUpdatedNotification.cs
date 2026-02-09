using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when account rate limits are updated.
/// </summary>
public sealed record class AccountRateLimitsUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the raw rate limits payload.
    /// </summary>
    public JsonElement RateLimits { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AccountRateLimitsUpdatedNotification"/>.
    /// </summary>
    public AccountRateLimitsUpdatedNotification(JsonElement RateLimits, JsonElement Params)
        : base("account/rateLimits/updated", Params)
    {
        this.RateLimits = RateLimits;
    }
}
