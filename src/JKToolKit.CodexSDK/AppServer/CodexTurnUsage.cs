using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>Last model-request and cumulative thread token usage, as reported by upstream.</summary>
public sealed record CodexTurnUsage
{
    /// <summary>Gets usage for the most recent model request; this is not necessarily the sum of a multi-request turn.</summary>
    public CodexTokenUsage? Last { get; init; }
    /// <summary>Gets cumulative thread usage.</summary>
    public CodexTokenUsage? Total { get; init; }
    /// <summary>Gets the model's context window, if reported.</summary>
    public long? ModelContextWindow { get; init; }
    /// <summary>Gets the unmodified usage payload, including future fields.</summary>
    public JsonElement Raw { get; init; }

    internal static CodexTurnUsage Parse(JsonElement raw) => new()
    {
        Last = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("last", out var last) ? CodexTokenUsage.Parse(last) : null,
        Total = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("total", out var total) ? CodexTokenUsage.Parse(total) : null,
        ModelContextWindow = CodexTokenUsage.Number(raw, "modelContextWindow"), Raw = raw.Clone()
    };
}

/// <summary>A 64-bit token usage breakdown. Missing counters remain null.</summary>
public sealed record CodexTokenUsage
{
    /// <summary>Gets input tokens.</summary>
    public long? InputTokens { get; init; }
    /// <summary>Gets cached input tokens.</summary>
    public long? CachedInputTokens { get; init; }
    /// <summary>Gets cache-write input tokens.</summary>
    public long? CacheWriteInputTokens { get; init; }
    /// <summary>Gets output tokens.</summary>
    public long? OutputTokens { get; init; }
    /// <summary>Gets reasoning output tokens.</summary>
    public long? ReasoningOutputTokens { get; init; }
    /// <summary>Gets total tokens.</summary>
    public long? TotalTokens { get; init; }

    internal static CodexTokenUsage? Parse(JsonElement raw) => raw.ValueKind != JsonValueKind.Object ? null : new()
    {
        InputTokens = Number(raw, "inputTokens"), CachedInputTokens = Number(raw, "cachedInputTokens"),
        CacheWriteInputTokens = Number(raw, "cacheWriteInputTokens"), OutputTokens = Number(raw, "outputTokens"),
        ReasoningOutputTokens = Number(raw, "reasoningOutputTokens"), TotalTokens = Number(raw, "totalTokens")
    };

    internal static long? Number(JsonElement raw, string name) => raw.ValueKind == JsonValueKind.Object &&
        raw.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out var value) ? value : null;
}
