using System.Linq;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Performs a one-shot fuzzy file search request.
    /// </summary>
    public async Task<IReadOnlyList<FuzzyFileSearchResult>> FuzzyFileSearchAsync(
        string query,
        IReadOnlyList<string> roots,
        string? cancellationToken = null,
        CancellationToken ct = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            throw new ArgumentException("Roots cannot be empty.", nameof(roots));
        }

        if (roots.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Roots cannot contain null, empty, or whitespace entries.", nameof(roots));
        }

        var response = await _core.SendRequestAsync(
            "fuzzyFileSearch",
            new FuzzyFileSearchParams
            {
                Query = query,
                Roots = roots.ToArray(),
                CancellationToken = string.IsNullOrWhiteSpace(cancellationToken) ? null : cancellationToken
            },
            ct).ConfigureAwait(false);

        return AppServerNotificationParsing.ParseFuzzyFileSearchResults(response);
    }

    /// <summary>
    /// Starts a fuzzy file search session (experimental).
    /// </summary>
    public Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct = default) =>
        _fuzzyFileSearchClient.StartFuzzyFileSearchSessionAsync(sessionId, roots, ct);

    /// <summary>
    /// Updates a fuzzy file search session query (experimental).
    /// </summary>
    public Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct = default) =>
        _fuzzyFileSearchClient.UpdateFuzzyFileSearchSessionAsync(sessionId, query, ct);

    /// <summary>
    /// Stops a fuzzy file search session (experimental).
    /// </summary>
    public Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct = default) =>
        _fuzzyFileSearchClient.StopFuzzyFileSearchSessionAsync(sessionId, ct);
}
