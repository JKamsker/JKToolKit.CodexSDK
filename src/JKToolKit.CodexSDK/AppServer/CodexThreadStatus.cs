using System;
using System.Collections.Generic;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a typed view over the status object returned with an app-server <c>Thread</c>.
/// </summary>
public sealed record class CodexThreadStatus
{
    /// <summary>
    /// Gets the upstream status type identifier (for example, <c>active</c>, <c>idle</c>, <c>notLoaded</c>, or <c>systemError</c>).
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the normalized status kind.
    /// </summary>
    public CodexThreadStatusKind Kind { get; }

    /// <summary>
    /// Gets the active flags reported alongside <see cref="CodexThreadStatusKind.Active" />.
    /// </summary>
    public IReadOnlyList<string>? ActiveFlags { get; }

    /// <summary>
    /// Gets the raw JSON payload for the status object.
    /// </summary>
    public JsonElement Raw { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CodexThreadStatus"/>.
    /// </summary>
    public CodexThreadStatus(string type, IReadOnlyList<string>? activeFlags, JsonElement raw)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        ActiveFlags = activeFlags;
        Raw = raw;
        Kind = MapKind(type);
    }

    /// <summary>
    /// Gets the normalized status floor for comparisons.
    /// </summary>
    private static CodexThreadStatusKind MapKind(string type) => type switch
    {
        "notLoaded" => CodexThreadStatusKind.NotLoaded,
        "idle" => CodexThreadStatusKind.Idle,
        "systemError" => CodexThreadStatusKind.SystemError,
        "active" => CodexThreadStatusKind.Active,
        _ => CodexThreadStatusKind.Unknown
    };
}

/// <summary>
/// Enumerates the canonical thread status kinds we currently recognize.
/// </summary>
public enum CodexThreadStatusKind
{
    /// <summary>
    /// Unknown or unsupported status type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Thread data is not loaded yet.
    /// </summary>
    NotLoaded,

    /// <summary>
    /// Thread is idle.
    /// </summary>
    Idle,

    /// <summary>
    /// Thread is actively running.
    /// </summary>
    Active,

    /// <summary>
    /// Thread is in an error state.
    /// </summary>
    SystemError
}
