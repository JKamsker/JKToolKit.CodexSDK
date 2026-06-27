using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents one external agent configuration import history entry.
/// </summary>
public sealed record class ExternalAgentConfigImportHistory
{
    /// <summary>
    /// Gets the import timestamp, when present.
    /// </summary>
    public string? ImportedAt { get; init; }

    /// <summary>
    /// Gets successful import item payloads.
    /// </summary>
    public IReadOnlyList<JsonElement> Successes { get; init; } = Array.Empty<JsonElement>();

    /// <summary>
    /// Gets failed import item payloads.
    /// </summary>
    public IReadOnlyList<JsonElement> Failures { get; init; } = Array.Empty<JsonElement>();

    /// <summary>
    /// Gets the raw history payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the result returned by <c>externalAgentConfig/import/readHistories</c>.
/// </summary>
public sealed record class ExternalAgentConfigImportHistoriesReadResult
{
    /// <summary>
    /// Gets the import histories.
    /// </summary>
    public required IReadOnlyList<ExternalAgentConfigImportHistory> Data { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
