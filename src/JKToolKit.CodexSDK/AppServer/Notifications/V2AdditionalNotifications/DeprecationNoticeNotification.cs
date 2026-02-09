using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted to inform clients about deprecated behavior or upcoming changes.
/// </summary>
public sealed record class DeprecationNoticeNotification : AppServerNotification
{
    /// <summary>
    /// Gets a short deprecation summary.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets optional details about the deprecation.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DeprecationNoticeNotification"/>.
    /// </summary>
    public DeprecationNoticeNotification(string Summary, string? Details, JsonElement Params)
        : base("deprecationNotice", Params)
    {
        this.Summary = Summary;
        this.Details = Details;
    }
}
