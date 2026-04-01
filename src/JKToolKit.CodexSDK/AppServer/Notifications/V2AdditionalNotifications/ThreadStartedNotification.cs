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
    /// Gets the parsed thread summary when the payload contains a recognizable thread shape.
    /// </summary>
    public CodexThreadSummary? ThreadSummary { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadStartedNotification"/>.
    /// </summary>
    public ThreadStartedNotification(JsonElement Thread, CodexThreadSummary? ThreadSummary, JsonElement Params)
        : base("thread/started", Params)
    {
        this.Thread = Thread;
        this.ThreadSummary = ThreadSummary;
    }

    /// <summary>
    /// Gets the thread identifier, if present in <see cref="Thread"/>.
    /// </summary>
    public string? ThreadId =>
        ThreadSummary?.ThreadId ??
        (Thread.ValueKind == JsonValueKind.Object &&
         Thread.TryGetProperty("id", out var id) &&
         id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null);
}
