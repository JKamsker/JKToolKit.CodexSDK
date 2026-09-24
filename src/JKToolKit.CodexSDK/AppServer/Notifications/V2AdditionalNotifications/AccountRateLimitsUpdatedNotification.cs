using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

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
        : base(AppServerMethods.AccountRateLimitsUpdated, Params)
    {
        this.RateLimits = RateLimits;
    }
}
