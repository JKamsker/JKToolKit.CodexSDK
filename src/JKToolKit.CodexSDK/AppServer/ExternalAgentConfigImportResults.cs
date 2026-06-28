using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Result returned by <c>externalAgentConfig/import</c>.
/// </summary>
public sealed record class ExternalAgentConfigImportResult
{
    /// <summary>
    /// Gets the upstream import identifier, when returned.
    /// </summary>
    public string? ImportId { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>externalAgentConfig/import/readHistories</c>.
/// </summary>
public sealed record class ExternalAgentConfigImportHistoriesReadResult
{
    /// <summary>
    /// Gets prior import history entries.
    /// </summary>
    public required IReadOnlyList<ExternalAgentConfigImportHistory> Data { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Prior external-agent import history entry.
/// </summary>
public sealed record class ExternalAgentConfigImportHistory
{
    /// <summary>
    /// Gets the upstream import identifier.
    /// </summary>
    public required string ImportId { get; init; }

    /// <summary>
    /// Gets the completion timestamp in Unix milliseconds.
    /// </summary>
    public long CompletedAtMs { get; init; }

    /// <summary>
    /// Gets raw success entries.
    /// </summary>
    public required IReadOnlyList<JsonElement> Successes { get; init; }

    /// <summary>
    /// Gets raw failure entries.
    /// </summary>
    public required IReadOnlyList<JsonElement> Failures { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the history entry.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
