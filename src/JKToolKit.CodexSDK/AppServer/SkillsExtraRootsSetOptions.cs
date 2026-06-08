using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>skills/extraRoots/set</c>.
/// </summary>
public sealed class SkillsExtraRootsSetOptions
{
    /// <summary>
    /// Gets or sets the absolute extra skill roots for the current app-server session.
    /// </summary>
    public IReadOnlyList<string> ExtraRoots { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Result returned by <c>skills/extraRoots/set</c>.
/// </summary>
public sealed record class SkillsExtraRootsSetResult
{
    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
