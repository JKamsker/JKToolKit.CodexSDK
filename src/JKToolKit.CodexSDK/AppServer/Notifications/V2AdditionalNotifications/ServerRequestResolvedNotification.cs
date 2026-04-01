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
    /// Gets the typed request identifier.
    /// </summary>
    public CodexRequestId RequestId { get; }

    /// <summary>
    /// Gets the raw request identifier payload.
    /// </summary>
    public JsonElement RequestIdRaw => RequestId.Raw;

    /// <summary>
    /// Gets the request identifier as text.
    /// </summary>
    public string RequestIdValue => RequestId.ValueText;

    /// <summary>
    /// Initializes a new instance of <see cref="ServerRequestResolvedNotification"/>.
    /// </summary>
    public ServerRequestResolvedNotification(string threadId, CodexRequestId requestId, JsonElement @params)
        : base("serverRequest/resolved", @params)
    {
        ThreadId = threadId;
        RequestId = requestId;
    }
}
