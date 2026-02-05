namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents a single plan step reported by Codex.
/// </summary>
/// <param name="Step">The step description.</param>
/// <param name="Status">The step status (e.g. pending, in_progress, completed).</param>
public sealed record PlanStep(string Step, string Status);
