using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a turn begins.
/// </summary>
public sealed record class TurnStartedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the raw turn payload.
    /// </summary>
    public JsonElement Turn { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TurnStartedNotification"/>.
    /// </summary>
    public TurnStartedNotification(string ThreadId, JsonElement Turn, JsonElement Params)
        : base(AppServerMethods.TurnStarted, Params)
    {
        this.ThreadId = ThreadId;
        this.Turn = Turn;
    }

    /// <summary>
    /// Gets the turn identifier, if present in <see cref="Turn"/>.
    /// </summary>
    public string? TurnId =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty(JsonFieldNames.Id, out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}
