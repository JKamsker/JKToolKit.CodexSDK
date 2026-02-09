using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted on Windows when world-writable paths are detected.
/// </summary>
public sealed record class WindowsWorldWritableWarningNotification : AppServerNotification
{
    /// <summary>
    /// Gets sample paths that were detected as world-writable.
    /// </summary>
    public IReadOnlyList<string> SamplePaths { get; }

    /// <summary>
    /// Gets the number of additional matches not included in <see cref="SamplePaths"/>.
    /// </summary>
    public int ExtraCount { get; }

    /// <summary>
    /// Gets a value indicating whether the scan failed or was incomplete.
    /// </summary>
    public bool FailedScan { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsWorldWritableWarningNotification"/>.
    /// </summary>
    public WindowsWorldWritableWarningNotification(
        IReadOnlyList<string> SamplePaths,
        int ExtraCount,
        bool FailedScan,
        JsonElement Params)
        : base("windows/worldWritableWarning", Params)
    {
        this.SamplePaths = SamplePaths ?? throw new ArgumentNullException(nameof(SamplePaths));
        this.ExtraCount = ExtraCount;
        this.FailedScan = FailedScan;
    }
}
