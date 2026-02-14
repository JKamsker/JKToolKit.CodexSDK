using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a single entry in a skills list response (typically scoped to a working directory).
/// </summary>
public sealed record class SkillsListEntryResult
{
    /// <summary>
    /// Gets the working directory for the entry, when present.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the listed skills.
    /// </summary>
    public required IReadOnlyList<SkillDescriptor> Skills { get; init; }

    /// <summary>
    /// Gets any errors reported for the entry.
    /// </summary>
    public required IReadOnlyList<CodexSkillErrorInfo> Errors { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the entry.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

