using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Known upstream thread goal statuses.
/// </summary>
public enum ThreadGoalStatus
{
    /// <summary>
    /// The status was not recognized by this SDK version.
    /// </summary>
    Unknown,

    /// <summary>
    /// The goal is active.
    /// </summary>
    Active,

    /// <summary>
    /// The goal is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// The goal is blocked.
    /// </summary>
    Blocked,

    /// <summary>
    /// The goal is paused because usage is limited.
    /// </summary>
    UsageLimited,

    /// <summary>
    /// The goal is paused because its token budget was reached.
    /// </summary>
    BudgetLimited,

    /// <summary>
    /// The goal is complete.
    /// </summary>
    Complete
}

/// <summary>
/// Options for <c>thread/goal/set</c>.
/// </summary>
public sealed class ThreadGoalSetOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the objective text to write.
    /// </summary>
    public string? Objective { get; set; }

    /// <summary>
    /// Gets or sets the status to write.
    /// </summary>
    public ThreadGoalStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets an optional token budget.
    /// </summary>
    public long? TokenBudget { get; set; }
}

/// <summary>
/// Represents the current thread goal state.
/// </summary>
public sealed record class ThreadGoal
{
    /// <summary>
    /// Gets the owning thread identifier.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the objective text.
    /// </summary>
    public required string Objective { get; init; }

    /// <summary>
    /// Gets the parsed status.
    /// </summary>
    public ThreadGoalStatus Status { get; init; }

    /// <summary>
    /// Gets the raw status string.
    /// </summary>
    public required string StatusValue { get; init; }

    /// <summary>
    /// Gets the optional token budget.
    /// </summary>
    public long? TokenBudget { get; init; }

    /// <summary>
    /// Gets the number of tokens used.
    /// </summary>
    public long TokensUsed { get; init; }

    /// <summary>
    /// Gets the number of seconds used.
    /// </summary>
    public long TimeUsedSeconds { get; init; }

    /// <summary>
    /// Gets the creation timestamp in upstream milliseconds/seconds form.
    /// </summary>
    public long CreatedAt { get; init; }

    /// <summary>
    /// Gets the update timestamp in upstream milliseconds/seconds form.
    /// </summary>
    public long UpdatedAt { get; init; }

    /// <summary>
    /// Gets the raw goal payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>thread/goal/set</c> or <c>thread/goal/get</c>.
/// </summary>
public sealed record class ThreadGoalResult
{
    /// <summary>
    /// Gets the goal, or <c>null</c> when no goal is set.
    /// </summary>
    public ThreadGoal? Goal { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>thread/goal/clear</c>.
/// </summary>
public sealed record class ThreadGoalClearResult
{
    /// <summary>
    /// Gets whether a stored goal was cleared.
    /// </summary>
    public bool Cleared { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
