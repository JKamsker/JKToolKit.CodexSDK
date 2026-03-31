using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a pending server request is resolved.
/// </summary>
public sealed record class ServerRequestResolvedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the raw request identifier payload.
    /// </summary>
    public JsonElement RequestId { get; }

    /// <summary>
    /// Gets a scalar request identifier when the upstream payload used a scalar value.
    /// </summary>
    public string? RequestIdValue { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ServerRequestResolvedNotification"/>.
    /// </summary>
    public ServerRequestResolvedNotification(string threadId, JsonElement requestId, string? requestIdValue, JsonElement @params)
        : base("serverRequest/resolved", @params)
    {
        ThreadId = threadId;
        RequestId = requestId;
        RequestIdValue = requestIdValue;
    }
}
