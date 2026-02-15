namespace JKToolKit.CodexSDK.AppServer;

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
    /// Gets the match indices when present.
    /// </summary>
    public IReadOnlyList<uint>? Indices { get; init; }
}

