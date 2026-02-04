using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class AccountRateLimitsUpdatedNotification : AppServerNotification
{
    public JsonElement RateLimits { get; }

    public AccountRateLimitsUpdatedNotification(JsonElement RateLimits, JsonElement Params)
        : base("account/rateLimits/updated", Params)
    {
        this.RateLimits = RateLimits;
    }
}

