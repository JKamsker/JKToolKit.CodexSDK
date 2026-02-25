using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the Windows sandbox setup flow completes.
/// </summary>
public sealed record class WindowsSandboxSetupCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the setup mode (for example, <c>elevated</c> or <c>unelevated</c>).
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// Gets a value indicating whether setup completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets an optional error message, when <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsSandboxSetupCompletedNotification"/>.
    /// </summary>
    public WindowsSandboxSetupCompletedNotification(string Mode, bool Success, string? Error, JsonElement Params)
        : base("windowsSandbox/setupCompleted", Params)
    {
        this.Mode = Mode;
        this.Success = Success;
        this.Error = Error;
    }
}
