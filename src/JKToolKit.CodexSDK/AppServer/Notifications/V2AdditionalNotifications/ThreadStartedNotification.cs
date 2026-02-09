using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a new thread is started or forked.
/// </summary>
public sealed record class ThreadStartedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the raw thread payload.
    /// </summary>
    public JsonElement Thread { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadStartedNotification"/>.
    /// </summary>
    public ThreadStartedNotification(JsonElement Thread, JsonElement Params)
        : base("thread/started", Params)
    {
        this.Thread = Thread;
    }

    /// <summary>
    /// Gets the thread identifier, if present in <see cref="Thread"/>.
    /// </summary>
    public string? ThreadId =>
        Thread.ValueKind == JsonValueKind.Object &&
        Thread.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}
