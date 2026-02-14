namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for steering an in-progress turn via <c>turn/steer</c>.
/// </summary>
public sealed class TurnSteerOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the expected turn identifier (precondition).
    /// </summary>
    public required string ExpectedTurnId { get; set; }

    /// <summary>
    /// Gets or sets the input items to append.
    /// </summary>
    public IReadOnlyList<TurnInputItem> Input { get; set; } = Array.Empty<TurnInputItem>();
}

