namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result status of a <c>thread/unsubscribe</c> request.
/// </summary>
public enum ThreadUnsubscribeStatus
{
    /// <summary>
    /// The status is unknown or could not be parsed.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The thread was not loaded in memory.
    /// </summary>
    NotLoaded = 1,

    /// <summary>
    /// The client was not subscribed to the thread.
    /// </summary>
    NotSubscribed = 2,

    /// <summary>
    /// The client was unsubscribed from the thread.
    /// </summary>
    Unsubscribed = 3
}

