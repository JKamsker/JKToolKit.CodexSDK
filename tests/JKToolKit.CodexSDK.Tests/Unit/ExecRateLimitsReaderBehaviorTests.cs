using System.Runtime.CompilerServices;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecRateLimitsReaderBehaviorTests
{
    [Fact]
    public async Task ReadsNewestSessionLastNonNullLimits_ThenCachesAndRefreshes()
    {
        var fixture = new Fixture();
        fixture.Sessions.AddRange([Session("older", 0), Session("newer", 2), Session("broken", 3), Session("empty", 1)]);
        var first = Limits(10);
        var last = Limits(20);
        fixture.Events["newer"] = [Token(first), ExecEventPipelineBehaviorTests.Event(), Token(last), Token(null)];
        fixture.Events["older"] = [Token(Limits(99))];
        fixture.Failures["broken"] = new IOException("file rotated");
        using var reader = new CodexClient(new CodexClientOptions(), sessionLocator: fixture, tailer: fixture, parser: fixture, pathProvider: fixture);
        Assert.Same(last, await reader.GetRateLimitsAsync(false, default));
        Assert.Equal(["broken", "newer"], fixture.ReadPaths);
        fixture.Events["newer"] = [Token(first)];
        Assert.Same(last, await reader.GetRateLimitsAsync(false, default));
        Assert.Equal(1, fixture.ListCount);
        Assert.Same(first, await reader.GetRateLimitsAsync(true, default));
        Assert.Equal(2, fixture.ListCount);
        Assert.All(fixture.Options, options =>
        {
            Assert.True(options.FromBeginning);
            Assert.False(options.Follow);
            Assert.Null(options.AfterTimestamp);
            Assert.Null(options.FromByteOffset);
        });
    }

    [Fact]
    public async Task MissingLimits_AreNotCached_AndAllCandidateLogsAreRead()
    {
        var fixture = new Fixture();
        fixture.Sessions.AddRange([Session("older", 0), Session("newer", 1)]);
        fixture.Events["newer"] = [Token(null)];
        var reader = fixture.Reader();
        Assert.Null(await reader.GetRateLimitsAsync(false, default));
        Assert.Equal(["newer", "older"], fixture.ReadPaths);
        var limits = Limits(42);
        fixture.Events["older"] = [Token(limits)];
        Assert.Same(limits, await reader.GetRateLimitsAsync(false, default));
        Assert.Equal(2, fixture.ListCount);
    }

    [Fact]
    public async Task ScanIsBoundedToFiftySessions()
    {
        var fixture = new Fixture();
        fixture.Sessions.AddRange(Enumerable.Range(0, 55).Select(i => Session(i.ToString(), i)));
        fixture.Events["50"] = [Token(Limits(90))];
        Assert.Null(await fixture.Reader().GetRateLimitsAsync(true, default));
        Assert.Equal(50, fixture.YieldCount);
        Assert.Equal(Enumerable.Range(0, 50).Reverse().Select(i => i.ToString()), fixture.ReadPaths);
    }

    [Fact]
    public async Task ReadCancellation_Propagates_AndReleasesCacheLock()
    {
        var fixture = new Fixture();
        fixture.Sessions.Add(Session("session", 0));
        fixture.Failures["session"] = new OperationCanceledException("tail stopped");
        var reader = fixture.Reader();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetRateLimitsAsync(false, default));
        fixture.Failures.Clear();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Null(await reader.GetRateLimitsAsync(false, deadline.Token));
        deadline.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetRateLimitsAsync(false, deadline.Token));
        Assert.Equal(2, fixture.ListCount);
    }

    private static CodexSessionInfo Session(string name, int seconds) => new(SessionId.Parse(name), name, DateTimeOffset.UnixEpoch.AddSeconds(seconds));
    private static RateLimits Limits(double percent) => new(new(percent, 300, null), null, null);
    private static TokenCountEvent Token(RateLimits? limits) => (TokenCountEvent)ExecEventPipelineBehaviorTests.Event() with { RateLimits = limits };

    private sealed class Fixture : ICodexSessionLocator, IJsonlTailer, IJsonlEventParser, ICodexPathProvider
    {
        public List<CodexSessionInfo> Sessions { get; } = [];
        public Dictionary<string, CodexEvent[]> Events { get; } = [];
        public Dictionary<string, Exception> Failures { get; } = [];
        public List<string> ReadPaths { get; } = [];
        public List<EventStreamOptions> Options { get; } = [];
        public int ListCount;
        public int YieldCount;
        public CodexRateLimitsReader Reader() => new(new CodexClientOptions(), this, this, this, this, NullLogger<CodexClient>.Instance);
        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string root, SessionFilter? filter, [EnumeratorCancellation] CancellationToken ct)
        {
            ListCount++;
            Assert.Equal("sessions-root", root);
            Assert.Null(filter);
            await Task.CompletedTask;
            foreach (var session in Sessions) { ct.ThrowIfCancellationRequested(); YieldCount++; yield return session; }
        }
        public async IAsyncEnumerable<string> TailAsync(string path, EventStreamOptions options, [EnumeratorCancellation] CancellationToken ct)
        {
            ReadPaths.Add(path);
            Options.Add(options);
            await Task.CompletedTask;
            ct.ThrowIfCancellationRequested();
            if (Failures.TryGetValue(path, out var failure)) throw failure;
            yield return path;
        }
        public async IAsyncEnumerable<CodexEvent> ParseAsync(IAsyncEnumerable<string> lines, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var path in lines.WithCancellation(ct))
                if (Events.TryGetValue(path, out var events))
                    foreach (var evt in events) yield return evt;
        }
        public string GetSessionsRootDirectory(string? directory) => "sessions-root";
        public string GetCodexExecutablePath(string? path) => throw new NotSupportedException();
        public string ResolveSessionLogPath(SessionId id, string? root) => throw new NotSupportedException();
        public Task<string> FindSessionLogAsync(SessionId id, string root, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> WaitForNewSessionFileAsync(string root, DateTimeOffset start, TimeSpan timeout, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> WaitForSessionLogByIdAsync(SessionId id, string root, TimeSpan timeout, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> ValidateLogFileAsync(string path, CancellationToken ct) => throw new NotSupportedException();
    }
}
