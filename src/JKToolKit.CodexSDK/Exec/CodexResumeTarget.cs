namespace JKToolKit.CodexSDK.Exec;

/// <summary>
/// Describes how <c>codex exec resume</c> should choose an existing session.
/// </summary>
/// <remarks>
/// Codex CLI accepts either a direct selector token (session id or thread name) or
/// <c>--last</c> to resume the most recent session. <c>--all</c> widens selection
/// beyond the current working-directory scope.
/// </remarks>
public sealed class CodexResumeTarget
{
    private CodexResumeTarget(string? selector, bool useMostRecent, bool includeAllSessions)
    {
        Selector = selector;
        UseMostRecent = useMostRecent;
        IncludeAllSessions = includeAllSessions;
    }

    /// <summary>
    /// Gets the selector token passed to the CLI when resuming by id or thread name.
    /// </summary>
    public string? Selector { get; }

    /// <summary>
    /// Gets a value indicating whether the CLI should resume the most recent session via <c>--last</c>.
    /// </summary>
    public bool UseMostRecent { get; }

    /// <summary>
    /// Gets a value indicating whether resume selection should use <c>--all</c>.
    /// </summary>
    public bool IncludeAllSessions { get; }

    /// <summary>
    /// Creates a target that resumes a specific selector token.
    /// </summary>
    /// <param name="selector">
    /// Selector token passed to <c>codex exec resume</c>. Upstream treats this as either a
    /// session id or a thread name, with id precedence when both match.
    /// </param>
    /// <param name="includeAllSessions">Whether to disable default cwd scoping via <c>--all</c>.</param>
    /// <returns>A validated resume target.</returns>
    public static CodexResumeTarget BySelector(string selector, bool includeAllSessions = false)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            throw new ArgumentException("Resume selector cannot be empty or whitespace.", nameof(selector));
        }

        return new CodexResumeTarget(selector, useMostRecent: false, includeAllSessions);
    }

    /// <summary>
    /// Creates a target that resumes the most recent session via <c>--last</c>.
    /// </summary>
    /// <param name="includeAllSessions">Whether to disable default cwd scoping via <c>--all</c>.</param>
    /// <returns>A validated resume target.</returns>
    public static CodexResumeTarget MostRecent(bool includeAllSessions = false) =>
        new(selector: null, useMostRecent: true, includeAllSessions);

    internal void Validate()
    {
        var hasSelector = !string.IsNullOrWhiteSpace(Selector);
        if (UseMostRecent != hasSelector)
        {
            return;
        }

        throw new InvalidOperationException(
            "Resume target must specify either a selector token or UseMostRecent, but not both.");
    }

    internal string Description =>
        UseMostRecent
            ? IncludeAllSessions ? "--last --all" : "--last"
            : IncludeAllSessions ? $"{Selector} --all" : Selector!;
}
