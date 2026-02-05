namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a plan update emitted by Codex.
/// </summary>
public sealed record PlanUpdateEvent : CodexEvent
{
    public string? Name { get; init; }
    public required IReadOnlyList<PlanStep> Plan { get; init; }
}

public sealed record PlanStep(string Step, string Status);

