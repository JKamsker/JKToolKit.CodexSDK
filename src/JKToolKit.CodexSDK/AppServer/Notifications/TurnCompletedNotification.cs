using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// Notification emitted when a turn reaches a terminal state.
/// </summary>
public sealed record class TurnCompletedNotification : AppServerNotification
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
    /// Initializes a new instance of <see cref="TurnCompletedNotification"/>.
    /// </summary>
    public TurnCompletedNotification(string ThreadId, JsonElement Turn, JsonElement Params)
        : base(AppServerMethods.TurnCompleted, Params)
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

    /// <summary>
    /// Gets the turn status string (for example, <c>completed</c>, <c>interrupted</c>, or <c>failed</c>), if present.
    /// </summary>
    public string? Status =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty(JsonFieldNames.Status, out var s) &&
        s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : null;

    /// <summary>
    /// Gets the error object, if present in <see cref="Turn"/>.
    /// </summary>
    public JsonElement? Error =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty(JsonFieldNames.Error, out var e) &&
        e.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
            ? e
            : null;
}


