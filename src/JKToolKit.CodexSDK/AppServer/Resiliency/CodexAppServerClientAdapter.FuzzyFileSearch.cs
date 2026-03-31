using System.Collections.Generic;
using System.Threading;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct);

    Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct);

    Task<IReadOnlyList<FuzzyFileSearchResult>> FuzzyFileSearchAsync(string query, IReadOnlyList<string> roots, string? cancellationToken, CancellationToken ct);

    Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct) => _inner.StartFuzzyFileSearchSessionAsync(sessionId, roots, ct);

    public Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct) => _inner.UpdateFuzzyFileSearchSessionAsync(sessionId, query, ct);

    public Task<IReadOnlyList<FuzzyFileSearchResult>> FuzzyFileSearchAsync(string query, IReadOnlyList<string> roots, string? cancellationToken, CancellationToken ct) =>
        _inner.FuzzyFileSearchAsync(query, roots, cancellationToken, ct);

    public Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct) => _inner.StopFuzzyFileSearchSessionAsync(sessionId, ct);
}
