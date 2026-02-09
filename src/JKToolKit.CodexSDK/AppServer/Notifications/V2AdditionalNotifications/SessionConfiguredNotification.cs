using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server reports the session configuration that applies to a thread.
/// </summary>
public sealed record class SessionConfiguredNotification : AppServerNotification
{
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the configured model identifier.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Gets the configured reasoning effort wire value, if present.
    /// </summary>
    public string? ReasoningEffort { get; }

    /// <summary>
    /// Gets the history log identifier.
    /// </summary>
    public long HistoryLogId { get; }

    /// <summary>
    /// Gets the number of history entries.
    /// </summary>
    public int HistoryEntryCount { get; }

    /// <summary>
    /// Gets the initial messages payload, if present.
    /// </summary>
    public JsonElement? InitialMessages { get; }

    /// <summary>
    /// Gets the rollout path for the session.
    /// </summary>
    /// <remarks>
    /// For ephemeral threads the server may omit this value (wire <c>null</c>).
    /// </remarks>
    public string? RolloutPath { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SessionConfiguredNotification"/>.
    /// </summary>
    public SessionConfiguredNotification(
        string SessionId,
        string Model,
        string? ReasoningEffort,
        long HistoryLogId,
        int HistoryEntryCount,
        JsonElement? InitialMessages,
        string? RolloutPath,
        JsonElement Params)
        : base("sessionConfigured", Params)
    {
        this.SessionId = SessionId;
        this.Model = Model;
        this.ReasoningEffort = ReasoningEffort;
        this.HistoryLogId = HistoryLogId;
        this.HistoryEntryCount = HistoryEntryCount;
        this.InitialMessages = InitialMessages;
        this.RolloutPath = RolloutPath;
    }
}
