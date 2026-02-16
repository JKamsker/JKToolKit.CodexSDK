using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerFuzzyFileSearchClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<bool> _experimentalApiEnabled;

    public CodexAppServerFuzzyFileSearchClient(
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<bool> experimentalApiEnabled)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _experimentalApiEnabled = experimentalApiEnabled ?? throw new ArgumentNullException(nameof(experimentalApiEnabled));
    }

    public async Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        ArgumentNullException.ThrowIfNull(roots);

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("fuzzyFileSearch/sessionStart");
        }

        _ = await _sendRequestAsync(
            "fuzzyFileSearch/sessionStart",
            new FuzzyFileSearchSessionStartParams
            {
                SessionId = sessionId,
                Roots = roots
            },
            ct);
    }

    public async Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty or whitespace.", nameof(query));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("fuzzyFileSearch/sessionUpdate");
        }

        _ = await _sendRequestAsync(
            "fuzzyFileSearch/sessionUpdate",
            new FuzzyFileSearchSessionUpdateParams
            {
                SessionId = sessionId,
                Query = query
            },
            ct);
    }

    public async Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("fuzzyFileSearch/sessionStop");
        }

        _ = await _sendRequestAsync(
            "fuzzyFileSearch/sessionStop",
            new FuzzyFileSearchSessionStopParams
            {
                SessionId = sessionId
            },
            ct);
    }
}

