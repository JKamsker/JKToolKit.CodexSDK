namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Known fuzzy file search match classifications.
/// </summary>
public enum FuzzyFileSearchMatchType
{
    /// <summary>
    /// The upstream match type was absent or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The match was driven by the file path.
    /// </summary>
    Path = 1,

    /// <summary>
    /// The match was driven by the file name.
    /// </summary>
    FileName = 2
}

/// <summary>
/// Represents a fuzzy file search match returned by the app-server.
/// </summary>
public sealed record class FuzzyFileSearchResult
{
    /// <summary>
    /// Gets the search root this file was matched under.
    /// </summary>
    public required string Root { get; init; }

    /// <summary>
    /// Gets the matched file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the matched file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the match score.
    /// </summary>
    public uint Score { get; init; }

    /// <summary>
    /// Gets the upstream match type when present.
    /// </summary>
    public string? MatchType { get; init; }

    /// <summary>
    /// Gets the parsed match classification.
    /// </summary>
    public FuzzyFileSearchMatchType MatchKind { get; init; }

    /// <summary>
    /// Gets the match indices when present.
    /// </summary>
    public IReadOnlyList<uint>? Indices { get; init; }
}
