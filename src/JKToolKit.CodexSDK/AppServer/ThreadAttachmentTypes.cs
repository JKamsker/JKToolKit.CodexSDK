using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>thread/attachment/add</c>.
/// </summary>
public sealed class ThreadAttachmentAddOptions
{
    /// <summary>
    /// Gets or sets the owning thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the application-defined attachment type.
    /// </summary>
    public required string AttachmentType { get; set; }

    /// <summary>
    /// Gets or sets the stable identity within the attachment type and thread.
    /// </summary>
    public required string IdentityKey { get; set; }

    /// <summary>
    /// Gets or sets the application-defined JSON payload.
    /// </summary>
    public required JsonElement Payload { get; set; }
}

/// <summary>
/// Describes the result of adding a thread attachment.
/// </summary>
public enum ThreadAttachmentAddOutcome
{
    /// <summary>
    /// The server returned an unrecognized outcome.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A new attachment was created.
    /// </summary>
    Created = 1,

    /// <summary>
    /// An attachment with the same identity already existed.
    /// </summary>
    Existing = 2
}

/// <summary>
/// Represents a stored thread attachment.
/// </summary>
public sealed record class ThreadAttachmentInfo
{
    /// <summary>
    /// Gets the server-assigned attachment identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the application-defined attachment type.
    /// </summary>
    public required string AttachmentType { get; init; }

    /// <summary>
    /// Gets the stable identity within the attachment type and thread.
    /// </summary>
    public required string IdentityKey { get; init; }

    /// <summary>
    /// Gets the application-defined JSON payload.
    /// </summary>
    public required JsonElement Payload { get; init; }

    /// <summary>
    /// Gets the attachment creation timestamp returned by the server.
    /// </summary>
    public long CreatedAt { get; init; }

    /// <summary>
    /// Gets the raw attachment payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>thread/attachment/add</c>.
/// </summary>
public sealed record class ThreadAttachmentAddResult
{
    /// <summary>
    /// Gets whether the attachment was created or already existed.
    /// </summary>
    public ThreadAttachmentAddOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the created or existing attachment.
    /// </summary>
    public required ThreadAttachmentInfo Attachment { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>thread/attachment/list</c>.
/// </summary>
public sealed class ThreadAttachmentListOptions
{
    /// <summary>
    /// Gets or sets the owning thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets an optional pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }
}

/// <summary>
/// Represents a page returned by <c>thread/attachment/list</c>.
/// </summary>
public sealed record class ThreadAttachmentListPage
{
    /// <summary>
    /// Gets the attachments returned for this page.
    /// </summary>
    public required IReadOnlyList<ThreadAttachmentInfo> Attachments { get; init; }

    /// <summary>
    /// Gets the next cursor token, if any.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>thread/attachment/remove</c>.
/// </summary>
public sealed class ThreadAttachmentRemoveOptions
{
    /// <summary>
    /// Gets or sets the owning thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the application-defined attachment type.
    /// </summary>
    public required string AttachmentType { get; set; }

    /// <summary>
    /// Gets or sets the stable identity within the attachment type and thread.
    /// </summary>
    public required string IdentityKey { get; set; }
}

/// <summary>
/// Result returned by <c>thread/attachment/remove</c>.
/// </summary>
public sealed record class ThreadAttachmentRemoveResult
{
    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
