#pragma warning disable CS1591

using System.Collections.Generic;
using System.Threading;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.FuzzyFileSearch, (c, token) => c.StartFuzzyFileSearchSessionAsync(sessionId, roots, token), ct);

    public Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.FuzzyFileSearch, (c, token) => c.UpdateFuzzyFileSearchSessionAsync(sessionId, query, token), ct);

    public Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.FuzzyFileSearch, (c, token) => c.StopFuzzyFileSearchSessionAsync(sessionId, token), ct);

    public Task<IReadOnlyList<FuzzyFileSearchResult>> FuzzyFileSearchAsync(
        string query,
        IReadOnlyList<string> roots,
        string? cancellationToken = null,
        CancellationToken ct = default) =>
        ExecuteAsync(
            CodexAppServerOperationKind.FuzzyFileSearch,
            (c, token) => c.FuzzyFileSearchAsync(query, roots, cancellationToken, token),
            ct);
}

#pragma warning restore CS1591
