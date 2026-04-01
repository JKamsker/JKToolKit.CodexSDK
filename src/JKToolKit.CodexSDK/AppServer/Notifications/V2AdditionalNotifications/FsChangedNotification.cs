using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a subscribed filesystem watch reports changed paths.
/// </summary>
public sealed record class FsChangedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the watch identifier returned by <c>fs/watch</c>.
    /// </summary>
    public string WatchId { get; }

    /// <summary>
    /// Gets the paths associated with this filesystem change event.
    /// </summary>
    public IReadOnlyList<string> ChangedPaths { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="FsChangedNotification"/>.
    /// </summary>
    public FsChangedNotification(string WatchId, IReadOnlyList<string> ChangedPaths, JsonElement Params)
        : base("fs/changed", Params)
    {
        this.WatchId = WatchId;
        this.ChangedPaths = ChangedPaths;
    }
}
