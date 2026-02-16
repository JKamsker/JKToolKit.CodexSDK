using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal sealed class CodexRateLimitsReader
{
    private readonly CodexClientOptions _clientOptions;
    private readonly ICodexSessionLocator _sessionLocator;
    private readonly IJsonlTailer _tailer;
    private readonly IJsonlEventParser _parser;
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILogger<CodexClient> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private RateLimits? _cachedRateLimits;
    private DateTimeOffset? _cachedRateLimitsTimestamp;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int MaxSessionsToScan = 50;

    internal CodexRateLimitsReader(
        CodexClientOptions clientOptions,
        ICodexSessionLocator sessionLocator,
        IJsonlTailer tailer,
        IJsonlEventParser parser,
        ICodexPathProvider pathProvider,
        ILogger<CodexClient> logger)
    {
        _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
        _sessionLocator = sessionLocator ?? throw new ArgumentNullException(nameof(sessionLocator));
        _tailer = tailer ?? throw new ArgumentNullException(nameof(tailer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task<RateLimits?> GetRateLimitsAsync(bool noCache, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _clientOptions.Validate();

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!noCache && _cachedRateLimits is not null && _cachedRateLimitsTimestamp.HasValue
                && (DateTimeOffset.UtcNow - _cachedRateLimitsTimestamp.Value) < CacheTtl)
            {
                return _cachedRateLimits;
            }

            var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);

            var mostRecent = new List<CodexSessionInfo>(capacity: MaxSessionsToScan);
            await foreach (var session in _sessionLocator.ListSessionsAsync(sessionsRoot, filter: null, cancellationToken))
            {
                TrackMostRecentSessions(mostRecent, session);
            }

            foreach (var session in mostRecent.OrderByDescending(s => s.CreatedAt))
            {
                var limits = await ReadLastRateLimitsAsync(session.LogPath, cancellationToken).ConfigureAwait(false);
                if (limits != null)
                {
                    _cachedRateLimits = limits;
                    _cachedRateLimitsTimestamp = DateTimeOffset.UtcNow;
                    return limits;
                }
            }

            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static void TrackMostRecentSessions(List<CodexSessionInfo> mostRecent, CodexSessionInfo candidate)
    {
        if (mostRecent.Count < MaxSessionsToScan)
        {
            mostRecent.Add(candidate);
            return;
        }

        var minIndex = 0;
        var minCreated = mostRecent[0].CreatedAt;
        for (var i = 1; i < mostRecent.Count; i++)
        {
            var created = mostRecent[i].CreatedAt;
            if (created < minCreated)
            {
                minCreated = created;
                minIndex = i;
            }
        }

        if (candidate.CreatedAt > minCreated)
        {
            mostRecent[minIndex] = candidate;
        }
    }

    private async Task<RateLimits?> ReadLastRateLimitsAsync(string logPath, CancellationToken cancellationToken)
    {
        var options = new EventStreamOptions(FromBeginning: true, AfterTimestamp: null, FromByteOffset: null, Follow: false);
        RateLimits? last = null;

        var lines = _tailer.TailAsync(logPath, options, cancellationToken);
        var events = _parser.ParseAsync(lines, cancellationToken);

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (evt is TokenCountEvent token && token.RateLimits is not null)
            {
                last = token.RateLimits;
            }
        }

        return last;
    }
}
