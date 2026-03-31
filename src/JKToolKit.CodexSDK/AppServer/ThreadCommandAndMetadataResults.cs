using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>thread/shellCommand</c>.
/// </summary>
public sealed class ThreadShellCommandOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the shell command string to execute.
    /// </summary>
    public required string Command { get; set; }
}

/// <summary>
/// Result returned by <c>thread/shellCommand</c>.
/// </summary>
public sealed record class ThreadShellCommandResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>thread/metadata/update</c>.
/// </summary>
public sealed class ThreadMetadataUpdateOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the Git metadata patch to apply.
    /// </summary>
    public ThreadGitInfoUpdate? GitInfo { get; set; }
}

/// <summary>
/// Git metadata fields that can be patched via <c>thread/metadata/update</c>.
/// </summary>
public sealed class ThreadGitInfoUpdate
{
    /// <summary>
    /// Gets or sets the branch value to write or clear.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// Gets or sets whether the branch field should be included in the patch.
    /// </summary>
    public bool UpdateBranch { get; set; }

    /// <summary>
    /// Gets or sets the origin URL value to write or clear.
    /// </summary>
    public string? OriginUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the origin URL field should be included in the patch.
    /// </summary>
    public bool UpdateOriginUrl { get; set; }

    /// <summary>
    /// Gets or sets the commit SHA value to write or clear.
    /// </summary>
    public string? Sha { get; set; }

    /// <summary>
    /// Gets or sets whether the commit SHA field should be included in the patch.
    /// </summary>
    public bool UpdateSha { get; set; }
}

/// <summary>
/// Result returned by <c>thread/metadata/update</c>.
/// </summary>
public sealed record class ThreadMetadataUpdateResult
{
    /// <summary>
    /// Gets the updated thread handle.
    /// </summary>
    public required CodexThread Thread { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>experimentalFeatureEnablement/set</c>.
/// </summary>
public sealed class ExperimentalFeatureEnablementSetOptions
{
    /// <summary>
    /// Gets or sets the feature enablement keyed by canonical feature name.
    /// </summary>
    public required IReadOnlyDictionary<string, bool> Enablement { get; set; }
}

/// <summary>
/// Result returned by <c>experimentalFeatureEnablement/set</c>.
/// </summary>
public sealed record class ExperimentalFeatureEnablementSetResult
{
    /// <summary>
    /// Gets the feature enablement entries updated by the request.
    /// </summary>
    public required IReadOnlyDictionary<string, bool> Enablement { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
