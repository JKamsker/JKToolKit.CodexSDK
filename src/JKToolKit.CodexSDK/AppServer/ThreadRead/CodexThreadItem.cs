using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Represents an item emitted during a turn in <c>thread/read</c> when turns are materialized.
/// </summary>
public abstract record class CodexThreadItem(string Id, string Type, JsonElement Raw)
{
    /// <summary>
    /// Gets the upstream item identifier.
    /// </summary>
    public string Id { get; } = Id ?? string.Empty;

    /// <summary>
    /// Gets the upstream item type discriminator.
    /// </summary>
    public string Type { get; } = Type ?? string.Empty;

    /// <summary>
    /// Gets the raw JSON payload for the item.
    /// </summary>
    public JsonElement Raw { get; } = Raw;

    /// <summary>
    /// Gets the Unix timestamp in milliseconds when the item started, when recorded.
    /// </summary>
    public long? StartedAtMs => GetOptionalInt64(Raw, "startedAtMs");

    /// <summary>
    /// Gets the Unix timestamp in milliseconds when the item completed, when recorded.
    /// </summary>
    public long? CompletedAtMs => GetOptionalInt64(Raw, "completedAtMs");

    private static long? GetOptionalInt64(JsonElement value, string propertyName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt64(out var result)
            ? result
            : null;
}

/// <summary>
/// Represents an uncategorized thread item.
/// </summary>
public sealed record class CodexThreadItemUnknown(string Id, string Type, JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);
