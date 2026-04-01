using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted when realtime transcript text changes.
/// </summary>
public sealed record class ThreadRealtimeTranscriptUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the transcript role associated with the text.
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Gets the updated transcript text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeTranscriptUpdatedNotification"/>.
    /// </summary>
    public ThreadRealtimeTranscriptUpdatedNotification(string ThreadId, string Role, string Text, JsonElement Params)
        : base("thread/realtime/transcriptUpdated", Params)
    {
        this.ThreadId = ThreadId;
        this.Role = Role;
        this.Text = Text;
    }
}
