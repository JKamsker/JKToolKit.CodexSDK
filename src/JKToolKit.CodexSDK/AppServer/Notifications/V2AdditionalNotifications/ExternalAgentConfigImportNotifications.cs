using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted while external agent configuration import is progressing.
/// </summary>
public sealed record class ExternalAgentConfigImportProgressNotification : AppServerNotification
{
    /// <summary>
    /// Gets the upstream import identifier.
    /// </summary>
    public string ImportId { get; }

    /// <summary>
    /// Gets raw item-type result entries.
    /// </summary>
    public IReadOnlyList<JsonElement> ItemTypeResults { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ExternalAgentConfigImportProgressNotification"/>.
    /// </summary>
    public ExternalAgentConfigImportProgressNotification(
        string ImportId,
        IReadOnlyList<JsonElement> ItemTypeResults,
        JsonElement Params)
        : base("externalAgentConfig/import/progress", Params)
    {
        this.ImportId = ImportId;
        this.ItemTypeResults = ItemTypeResults;
    }
}

/// <summary>
/// Notification emitted when external agent configuration import completes.
/// </summary>
public sealed record class ExternalAgentConfigImportCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the upstream import identifier.
    /// </summary>
    public string ImportId { get; }

    /// <summary>
    /// Gets raw item-type result entries.
    /// </summary>
    public IReadOnlyList<JsonElement> ItemTypeResults { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ExternalAgentConfigImportCompletedNotification"/>.
    /// </summary>
    public ExternalAgentConfigImportCompletedNotification(
        string ImportId,
        IReadOnlyList<JsonElement> ItemTypeResults,
        JsonElement Params)
        : base("externalAgentConfig/import/completed", Params)
    {
        this.ImportId = ImportId;
        this.ItemTypeResults = ItemTypeResults;
    }
}
