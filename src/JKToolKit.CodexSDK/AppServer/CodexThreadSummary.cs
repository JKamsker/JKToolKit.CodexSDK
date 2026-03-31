using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a best-effort typed summary of a thread returned by the app-server.
/// </summary>
public sealed record class CodexThreadSummary
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the optional thread name/title, when present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the thread is archived, when present.
    /// </summary>
    public bool? Archived { get; init; }

    /// <summary>
    /// Gets the upstream thread status type, when present.
    /// </summary>
    /// <remarks>
    /// Known values include <c>notLoaded</c>, <c>idle</c>, <c>active</c>, and <c>systemError</c>.
    /// </remarks>
    public string? StatusType { get; init; }

    /// <summary>
    /// Gets active status flags, when present.
    /// </summary>
    /// <remarks>
    /// Only present when <see cref="StatusType"/> is <c>active</c>.
    /// Known values include <c>waitingOnApproval</c> and <c>waitingOnUserInput</c>.
    /// </remarks>
    public IReadOnlyList<string>? ActiveFlags { get; init; }

    /// <summary>
    /// Gets the optional thread preview text, when present.
    /// </summary>
    public string? Preview { get; init; }

    /// <summary>
    /// Gets the thread creation timestamp, when present.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the thread last-updated timestamp, when present.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the working directory associated with the thread, when present.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the rollout path associated with the thread, when present.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the model associated with the thread, when present.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the model provider associated with the thread, when present.
    /// </summary>
    public string? ModelProvider { get; init; }

    /// <summary>
    /// Gets the service tier associated with the thread, when present.
    /// </summary>
    public CodexServiceTier? ServiceTier { get; init; }

    /// <summary>
    /// Gets a value indicating whether the thread is ephemeral, when present.
    /// </summary>
    public bool? Ephemeral { get; init; }

    /// <summary>
    /// Gets the thread source kind, when present.
    /// </summary>
    public string? SourceKind { get; init; }

    /// <summary>
    /// Gets the CLI version that created the thread, when present.
    /// </summary>
    public string? CliVersion { get; init; }

    /// <summary>
    /// Gets the optional agent nickname for sub-agent threads, when present.
    /// </summary>
    public string? AgentNickname { get; init; }

    /// <summary>
    /// Gets the optional agent role for sub-agent threads, when present.
    /// </summary>
    public string? AgentRole { get; init; }

    /// <summary>
    /// Gets the number of turns materialized on the thread payload, when present.
    /// </summary>
    public int? TurnCount { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the thread summary.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
