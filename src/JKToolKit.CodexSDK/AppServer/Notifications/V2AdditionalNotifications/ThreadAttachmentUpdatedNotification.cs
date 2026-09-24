using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Describes a persisted thread attachment change.
/// </summary>
public enum ThreadAttachmentOperation
{
    /// <summary>
    /// The server returned an unrecognized operation.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The attachment was created.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The attachment was deleted.
    /// </summary>
    Deleted = 2
}

/// <summary>
/// Notification emitted after a thread attachment is created or deleted.
/// </summary>
public sealed record class ThreadAttachmentUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the owning thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the application-defined attachment type.
    /// </summary>
    public string AttachmentType { get; }

    /// <summary>
    /// Gets the stable identity within the attachment type and thread.
    /// </summary>
    public string IdentityKey { get; }

    /// <summary>
    /// Gets the server-assigned attachment identifier.
    /// </summary>
    public string AttachmentId { get; }

    /// <summary>
    /// Gets the persisted attachment operation.
    /// </summary>
    public ThreadAttachmentOperation Operation { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadAttachmentUpdatedNotification"/>.
    /// </summary>
    public ThreadAttachmentUpdatedNotification(
        string threadId,
        string attachmentType,
        string identityKey,
        string attachmentId,
        ThreadAttachmentOperation operation,
        JsonElement @params)
        : base(AppServerMethods.ThreadAttachmentUpdated, @params)
    {
        ThreadId = threadId;
        AttachmentType = attachmentType;
        IdentityKey = identityKey;
        AttachmentId = attachmentId;
        Operation = operation;
    }
}
