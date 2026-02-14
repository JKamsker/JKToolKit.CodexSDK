using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>skills/list</c> request (v2 protocol).
/// </summary>
public sealed record class SkillsListParams
{
    /// <summary>
    /// Gets optional working directories to scope the scan to.
    /// </summary>
    /// <remarks>
    /// When empty or omitted, the server defaults to the current session working directory.
    /// </remarks>
    [JsonPropertyName("cwds")]
    public IReadOnlyList<string>? Cwds { get; init; }

    /// <summary>
    /// Gets a value indicating whether to bypass the skills cache and re-scan skills from disk.
    /// </summary>
    [JsonPropertyName("forceReload")]
    public bool? ForceReload { get; init; }

    /// <summary>
    /// Gets optional per-cwd extra roots to scan as user-scoped skills.
    /// </summary>
    [JsonPropertyName("perCwdExtraUserRoots")]
    public IReadOnlyList<SkillsListExtraRootsForCwd>? PerCwdExtraUserRoots { get; init; }
}

/// <summary>
/// Wire entry used by <see cref="SkillsListParams.PerCwdExtraUserRoots"/>.
/// </summary>
public sealed record class SkillsListExtraRootsForCwd
{
    /// <summary>
    /// Gets the working directory this entry applies to.
    /// </summary>
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }

    /// <summary>
    /// Gets extra roots for scanning user-scoped skills.
    /// </summary>
    [JsonPropertyName("extraUserRoots")]
    public required IReadOnlyList<string> ExtraUserRoots { get; init; }
}

