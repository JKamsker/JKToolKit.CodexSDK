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

    /// <summary>
    /// Gets or sets an optional client-provided id for the user message item created by this steer request.
    /// </summary>
    public string? ClientUserMessageId { get; set; }

    /// <summary>
    /// Gets or sets optional turn-scoped Responses API client metadata.
    /// </summary>
    /// <remarks>
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public IReadOnlyDictionary<string, string>? ResponsesApiClientMetadata { get; set; }

    /// <summary>
    /// Gets or sets optional client-provided context fragments keyed by opaque source identifier.
    /// </summary>
    /// <remarks>
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public IReadOnlyDictionary<string, TurnAdditionalContextEntry>? AdditionalContext { get; set; }
}

