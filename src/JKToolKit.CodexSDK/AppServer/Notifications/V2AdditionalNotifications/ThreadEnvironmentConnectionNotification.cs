using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread environment connects or disconnects.
/// </summary>
public sealed record class ThreadEnvironmentConnectionNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the environment identifier.
    /// </summary>
    public string EnvironmentId { get; }

    /// <summary>
    /// Gets a value indicating whether the environment is connected.
    /// </summary>
    public bool Connected { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadEnvironmentConnectionNotification"/>.
    /// </summary>
    public ThreadEnvironmentConnectionNotification(string method, string threadId, string environmentId, JsonElement @params)
        : base(method, @params)
    {
        ThreadId = threadId;
        EnvironmentId = environmentId;
        Connected = method == "thread/environment/connected";
    }
}
