namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents a completed turn item (e.g. plan, web search, reasoning).
/// </summary>
public sealed record TurnItemCompletedEvent : CodexEvent
{
    /// <summary>
    /// Gets the thread id, when provided.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets the turn id, when provided.
    /// </summary>
    public string? TurnId { get; init; }

    /// <summary>
    /// Gets the completed item type discriminator (e.g. <c>Plan</c>), when provided.
    /// </summary>
    public string? ItemType { get; init; }

    /// <summary>
    /// Gets the item id, when provided.
    /// </summary>
    public string? ItemId { get; init; }

    /// <summary>
    /// Gets a best-effort text payload for the item (e.g. plan text), when available.
    /// </summary>
    public string? Text { get; init; }
}

