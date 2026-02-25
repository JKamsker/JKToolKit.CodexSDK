using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread's status changes.
/// </summary>
public sealed record class ThreadStatusChangedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the raw status payload.
    /// </summary>
    public JsonElement Status { get; }

    /// <summary>
    /// Gets the status type, if present (for example, <c>active</c>, <c>idle</c>, <c>notLoaded</c>, <c>systemError</c>).
    /// </summary>
    public string? StatusType { get; }

    /// <summary>
    /// Gets active status flags, if present (for example, <c>waitingOnApproval</c>, <c>waitingOnUserInput</c>).
    /// </summary>
    public IReadOnlyList<string>? ActiveFlags { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadStatusChangedNotification"/>.
    /// </summary>
    public ThreadStatusChangedNotification(string ThreadId, JsonElement Status, JsonElement Params)
        : base("thread/status/changed", Params)
    {
        this.ThreadId = ThreadId;
        this.Status = Status;
        StatusType = TryGetString(Status, "type");
        ActiveFlags = TryGetStringArray(Status, "activeFlags");
    }

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static IReadOnlyList<string>? TryGetStringArray(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
    }
}
