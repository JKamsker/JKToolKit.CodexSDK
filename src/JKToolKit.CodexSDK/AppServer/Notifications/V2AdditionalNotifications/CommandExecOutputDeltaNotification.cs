using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a connection-scoped <c>command/exec</c> request streams output.
/// </summary>
public sealed record class CommandExecOutputDeltaNotification : AppServerNotification
{
    /// <summary>
    /// Gets the client-supplied process identifier for the command execution.
    /// </summary>
    public string ProcessId { get; }

    /// <summary>
    /// Gets the output stream label (<c>stdout</c> or <c>stderr</c>).
    /// </summary>
    public string Stream { get; }

    /// <summary>
    /// Gets the base64-encoded output bytes.
    /// </summary>
    public string DeltaBase64 { get; }

    /// <summary>
    /// Gets a value indicating whether the output cap was reached for this stream.
    /// </summary>
    public bool CapReached { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CommandExecOutputDeltaNotification"/>.
    /// </summary>
    public CommandExecOutputDeltaNotification(
        string ProcessId,
        string Stream,
        string DeltaBase64,
        bool CapReached,
        JsonElement Params)
        : base("command/exec/outputDelta", Params)
    {
        this.ProcessId = ProcessId;
        this.Stream = Stream;
        this.DeltaBase64 = DeltaBase64;
        this.CapReached = CapReached;
    }
}
