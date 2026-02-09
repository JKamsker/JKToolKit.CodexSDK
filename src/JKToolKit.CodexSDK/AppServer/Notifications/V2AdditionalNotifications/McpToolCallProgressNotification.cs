using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted to report progress for an <c>mcpToolCall</c> item.
/// </summary>
public sealed record class McpToolCallProgressNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Gets the item identifier that the progress message applies to.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the progress message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="McpToolCallProgressNotification"/>.
    /// </summary>
    public McpToolCallProgressNotification(string ThreadId, string TurnId, string ItemId, string Message, JsonElement Params)
        : base("item/mcpToolCall/progress", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Message = Message;
    }
}
