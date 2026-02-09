namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents a plan update emitted by Codex.
/// </summary>
public sealed record PlanUpdateEvent : CodexEvent
{
    /// <summary>
    /// Gets the optional plan name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the updated plan steps.
    /// </summary>
    public required IReadOnlyList<PlanStep> Plan { get; init; }
}

