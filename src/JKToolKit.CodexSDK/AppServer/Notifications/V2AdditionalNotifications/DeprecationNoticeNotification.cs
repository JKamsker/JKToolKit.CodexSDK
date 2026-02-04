using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class DeprecationNoticeNotification : AppServerNotification
{
    public string Summary { get; }
    public string? Details { get; }

    public DeprecationNoticeNotification(string Summary, string? Details, JsonElement Params)
        : base("deprecationNotice", Params)
    {
        this.Summary = Summary;
        this.Details = Details;
    }
}

