using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class WindowsWorldWritableWarningNotification : AppServerNotification
{
    public IReadOnlyList<string> SamplePaths { get; }
    public int ExtraCount { get; }
    public bool FailedScan { get; }

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

