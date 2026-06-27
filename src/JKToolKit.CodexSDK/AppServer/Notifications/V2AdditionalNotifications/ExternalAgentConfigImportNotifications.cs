namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted while external agent configuration import is progressing.
/// </summary>
public sealed record class ExternalAgentConfigImportProgressNotification : AppServerNotification
{
    /// <summary>
    /// Gets the raw item-type results payload.
    /// </summary>
    public System.Text.Json.JsonElement ItemTypeResults { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ExternalAgentConfigImportProgressNotification"/>.
    /// </summary>
    public ExternalAgentConfigImportProgressNotification(
        System.Text.Json.JsonElement ItemTypeResults,
        System.Text.Json.JsonElement Params)
        : base("externalAgentConfig/import/progress", Params)
    {
        this.ItemTypeResults = ItemTypeResults;
    }
}

/// <summary>
/// Notification emitted when external agent configuration import completes.
/// </summary>
public sealed record class ExternalAgentConfigImportCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the raw item-type results payload.
    /// </summary>
    public System.Text.Json.JsonElement ItemTypeResults { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ExternalAgentConfigImportCompletedNotification"/>.
    /// </summary>
    public ExternalAgentConfigImportCompletedNotification(
        System.Text.Json.JsonElement ItemTypeResults,
        System.Text.Json.JsonElement Params)
        : base("externalAgentConfig/import/completed", Params)
    {
        this.ItemTypeResults = ItemTypeResults;
    }
}
