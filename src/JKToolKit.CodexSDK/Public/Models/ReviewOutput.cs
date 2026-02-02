namespace JKToolKit.CodexSDK.Public.Models;

/// <summary>
/// Structured output produced by Codex review mode.
/// </summary>
public sealed record ReviewOutput(
    string? OverallCorrectness,
    string? OverallExplanation,
    double? OverallConfidenceScore,
    IReadOnlyList<ReviewFinding> Findings);

/// <summary>
/// Represents a single review finding.
/// </summary>
public sealed record ReviewFinding(
    int? Priority,
    double? ConfidenceScore,
    string? Title,
    string? Body,
    ReviewCodeLocation? CodeLocation);

/// <summary>
/// Represents the code location associated with a finding.
/// </summary>
public sealed record ReviewCodeLocation(
    string? AbsoluteFilePath,
    ReviewLineRange? LineRange);

/// <summary>
/// Represents a 1-based line range.
/// </summary>
public sealed record ReviewLineRange(int? Start, int? End);

