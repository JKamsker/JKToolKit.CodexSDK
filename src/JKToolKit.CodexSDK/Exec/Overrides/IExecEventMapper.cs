using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;

namespace JKToolKit.CodexSDK.Exec.Overrides;

/// <summary>
/// Maps JSONL events (by <c>type</c>) to typed <see cref="CodexEvent"/> objects.
/// </summary>
/// <remarks>
/// This enables consumers to override or extend the SDK's JSONL event mapping for forward compatibility
/// when upstream Codex adds new event shapes.
/// </remarks>
public interface IExecEventMapper
{
    /// <summary>
    /// Attempts to map an event to a typed instance.
    /// </summary>
    /// <param name="timestamp">The event timestamp.</param>
    /// <param name="type">The event type identifier.</param>
    /// <param name="rawPayload">The raw JSON payload (never null).</param>
    /// <returns>A mapped event, or null if this mapper does not handle the event.</returns>
    CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload);
}

