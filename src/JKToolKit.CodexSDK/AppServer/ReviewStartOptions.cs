namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for starting a review via the app-server.
/// </summary>
public sealed class ReviewStartOptions
{
    /// <summary>
    /// Gets or sets the thread identifier to run the review from.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the review target.
    /// </summary>
    public required ReviewTarget Target { get; set; }

    /// <summary>
    /// Gets or sets an optional delivery mode.
    /// </summary>
    public ReviewDelivery? Delivery { get; set; }
}

