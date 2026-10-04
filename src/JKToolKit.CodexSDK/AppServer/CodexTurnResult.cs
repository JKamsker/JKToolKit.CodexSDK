using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>A collected terminal turn, independent of event stream consumption.</summary>
public sealed record CodexTurnResult
{
    /// <summary>Gets the owning thread identifier.</summary>
    public required string ThreadId { get; init; }
    /// <summary>Gets the turn identifier.</summary>
    public required string TurnId { get; init; }
    /// <summary>Gets the last final agent message (or last unlabelled agent message).</summary>
    public string FinalResponse { get; init; } = string.Empty;
    /// <summary>Gets completed items in arrival order, replaced by identifier when updated.</summary>
    public IReadOnlyList<CodexThreadItem> Items { get; init; } = Array.Empty<CodexThreadItem>();
    /// <summary>Gets the latest upstream token usage, including last-turn and cumulative usage.</summary>
    public CodexTurnUsage? Usage { get; init; }
    /// <summary>Gets the upstream terminal status, including unknown future values.</summary>
    public string? Status { get; init; }
    /// <summary>Gets the upstream terminal error, if any.</summary>
    public JsonElement? Error { get; init; }
    /// <summary>Gets the latest aggregated unified diff, if reported.</summary>
    public string? Diff { get; init; }
    /// <summary>Gets the full terminal turn payload for diagnostics.</summary>
    public JsonElement TerminalTurn { get; init; }
    /// <summary>Gets whether pre-registration notifications were dropped; items or usage may be incomplete.</summary>
    public bool IsPartial { get; init; }
}
