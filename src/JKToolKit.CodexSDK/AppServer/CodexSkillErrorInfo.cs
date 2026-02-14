using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a best-effort typed error info entry returned by skills listing APIs.
/// </summary>
public sealed record class CodexSkillErrorInfo
{
    /// <summary>
    /// Gets the error message, when present.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets an optional path associated with the error, when present.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the error entry.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

