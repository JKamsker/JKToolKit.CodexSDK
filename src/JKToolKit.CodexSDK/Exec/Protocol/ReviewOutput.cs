namespace JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Structured output produced by Codex review mode.
/// </summary>
/// <param name="OverallCorrectness">Overall correctness summary, when provided.</param>
/// <param name="OverallExplanation">Overall explanation text, when provided.</param>
/// <param name="OverallConfidenceScore">Overall confidence score, when provided.</param>
/// <param name="Findings">Collection of review findings.</param>
public sealed record ReviewOutput(
    string? OverallCorrectness,
    string? OverallExplanation,
    double? OverallConfidenceScore,
    IReadOnlyList<ReviewFinding> Findings);

/// <summary>
/// Represents a single review finding.
/// </summary>
/// <param name="Priority">Finding priority, when provided.</param>
/// <param name="ConfidenceScore">Confidence score, when provided.</param>
/// <param name="Title">Short title, when provided.</param>
/// <param name="Body">Detailed body text, when provided.</param>
/// <param name="CodeLocation">Associated code location, when provided.</param>
public sealed record ReviewFinding(
    int? Priority,
    double? ConfidenceScore,
    string? Title,
    string? Body,
    ReviewCodeLocation? CodeLocation);

/// <summary>
/// Represents the code location associated with a finding.
/// </summary>
/// <param name="AbsoluteFilePath">Absolute file path, when provided.</param>
/// <param name="LineRange">Line range in the file, when provided.</param>
public sealed record ReviewCodeLocation(
    string? AbsoluteFilePath,
    ReviewLineRange? LineRange);

/// <summary>
/// Represents a 1-based line range.
/// </summary>
/// <param name="Start">Start line (inclusive), when provided.</param>
/// <param name="End">End line (inclusive), when provided.</param>
public sealed record ReviewLineRange(int? Start, int? End);

