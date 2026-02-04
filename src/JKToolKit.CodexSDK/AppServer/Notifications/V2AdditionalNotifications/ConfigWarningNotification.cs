using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ConfigWarningNotification : AppServerNotification
{
    public string Summary { get; }
    public string? Details { get; }
    public string? Path { get; }
    public JsonElement? Range { get; }

    public ConfigWarningNotification(string Summary, string? Details, string? Path, JsonElement? Range, JsonElement Params)
        : base("configWarning", Params)
    {
        this.Summary = Summary;
        this.Details = Details;
        this.Path = Path;
        this.Range = Range;
    }
}

