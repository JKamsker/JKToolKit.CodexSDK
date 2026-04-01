using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents Git metadata captured on a thread snapshot.
/// </summary>
public sealed record class CodexThreadGitInfo
{
    /// <summary>
    /// Gets the commit SHA, when present.
    /// </summary>
    public string? Sha { get; init; }

    /// <summary>
    /// Gets the branch name, when present.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Gets the origin URL, when present.
    /// </summary>
    public string? OriginUrl { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the Git metadata object.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
