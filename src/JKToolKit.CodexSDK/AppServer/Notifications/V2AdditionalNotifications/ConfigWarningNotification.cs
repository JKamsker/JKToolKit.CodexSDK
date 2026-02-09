using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server encounters a configuration warning.
/// </summary>
public sealed record class ConfigWarningNotification : AppServerNotification
{
    /// <summary>
    /// Gets a short warning summary.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets optional warning details.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Gets an optional path related to the warning (for example, a config file path).
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets an optional range payload associated with the warning.
    /// </summary>
    public JsonElement? Range { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigWarningNotification"/>.
    /// </summary>
    public ConfigWarningNotification(string Summary, string? Details, string? Path, JsonElement? Range, JsonElement Params)
        : base("configWarning", Params)
    {
        this.Summary = Summary;
        this.Details = Details;
        this.Path = Path;
        this.Range = Range;
    }
}
