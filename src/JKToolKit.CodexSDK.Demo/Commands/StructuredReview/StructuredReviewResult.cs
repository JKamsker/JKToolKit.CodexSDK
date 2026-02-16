namespace JKToolKit.CodexSDK.Demo.Commands.StructuredReview;

/// <summary>
/// DTO returned by a structured code review.
/// </summary>
public sealed record StructuredReviewResult
{
    /// <summary>
    /// Overall review summary describing what was reviewed and the general findings.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Severity of the overall review: "clean", "low", "medium", "high", or "critical".
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Individual issues found during the review.
    /// </summary>
    public required List<ReviewIssue> Issues { get; init; }

    /// <summary>
    /// Ordered list of tasks to fix the issues found. Empty when the review is clean.
    /// </summary>
    public required List<FixTask> FixTasks { get; init; }
}

/// <summary>
/// A single issue found during code review.
/// </summary>
public sealed record ReviewIssue
{
    /// <summary>
    /// File path where the issue was found.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Line number or range (e.g. "42" or "42-50").
    /// </summary>
    public string? LineRange { get; init; }

    /// <summary>
    /// Category: "bug", "security", "performance", "style", "maintainability", or "other".
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Severity: "info", "warning", "error", or "critical".
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Description of the issue.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// A concrete task to fix one or more review issues.
/// </summary>
public sealed record FixTask
{
    /// <summary>
    /// Short title of the fix task.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed description of what to do.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Priority: 1 (highest) to 5 (lowest).
    /// </summary>
    public required int Priority { get; init; }

    /// <summary>
    /// File paths affected by this task.
    /// </summary>
    public required List<string> AffectedFiles { get; init; }
}
