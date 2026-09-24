using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when upstream sends turn moderation metadata.
/// </summary>
public sealed record class TurnModerationMetadataNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread id.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn id.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Gets the moderation metadata payload.
    /// </summary>
    public JsonElement Metadata { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TurnModerationMetadataNotification"/>.
    /// </summary>
    public TurnModerationMetadataNotification(string ThreadId, string TurnId, JsonElement Metadata, JsonElement Params)
        : base(AppServerMethods.TurnModerationMetadata, Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Metadata = Metadata;
    }
}
