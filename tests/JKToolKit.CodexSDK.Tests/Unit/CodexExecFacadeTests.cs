using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using FluentAssertions;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class CodexExecFacadeTests
{
    [Fact]
    public async Task StartSessionAsync_DelegatesToClient()
    {
        var expectedHandle = new FakeSessionHandle();
        var client = new FakeCodexClient { StartSessionResult = expectedHandle };
        var facade = new CodexExecFacade(client);

        var options = new CodexSessionOptions(@"C:\repo", "hi");
        using var cts = new CancellationTokenSource();

        var actual = await facade.StartSessionAsync(options, cts.Token);

        actual.Should().BeSameAs(expectedHandle);
        client.StartSessionCalls.Should().ContainSingle().Which.Should().Be((options, cts.Token));
    }

    [Fact]
    public async Task ResumeSessionAsync_WithOptions_DelegatesToClient()
    {
        var expectedHandle = new FakeSessionHandle();
        var client = new FakeCodexClient { ResumeWithOptionsResult = expectedHandle };
        var facade = new CodexExecFacade(client);

        var sessionId = SessionId.Parse("abc123");
        var options = new CodexSessionOptions(@"C:\repo", "follow up");
        using var cts = new CancellationTokenSource();

        var actual = await facade.ResumeSessionAsync(sessionId, options, cts.Token);

        actual.Should().BeSameAs(expectedHandle);
        client.ResumeWithOptionsCalls.Should().ContainSingle().Which.Should().Be((sessionId, options, cts.Token));
    }

    [Fact]
    public async Task ResumeSessionAsync_ReadOnly_DelegatesToClient()
    {
        var expectedHandle = new FakeSessionHandle();
        var client = new FakeCodexClient { ResumeReadOnlyResult = expectedHandle };
        var facade = new CodexExecFacade(client);

        var sessionId = SessionId.Parse("abc123");
        using var cts = new CancellationTokenSource();

        var actual = await facade.ResumeSessionAsync(sessionId, cts.Token);

        actual.Should().BeSameAs(expectedHandle);
        client.ResumeReadOnlyCalls.Should().ContainSingle().Which.Should().Be((sessionId, cts.Token));
    }

    [Fact]
    public async Task AttachToLogAsync_DelegatesToClient()
    {
        var expectedHandle = new FakeSessionHandle();
        var client = new FakeCodexClient { AttachToLogResult = expectedHandle };
        var facade = new CodexExecFacade(client);

        using var cts = new CancellationTokenSource();
        var logPath = @"C:\logs\session.jsonl";

        var actual = await facade.AttachToLogAsync(logPath, cts.Token);

        actual.Should().BeSameAs(expectedHandle);
        client.AttachToLogCalls.Should().ContainSingle().Which.Should().Be((logPath, cts.Token));
    }

    [Fact]
    public async Task ListSessionsAsync_DelegatesToClient()
    {
        var sentinel = new TestAsyncEnumerable<CodexSessionInfo>();
        var client = new FakeCodexClient { ListSessionsResult = sentinel };
        var facade = new CodexExecFacade(client);

        using var cts = new CancellationTokenSource();
        var filter = new SessionFilter();

        var actual = facade.ListSessionsAsync(filter, cts.Token);
        actual.Should().BeSameAs(sentinel);

        // Enumerate once to ensure the underlying enumerable is usable.
        await foreach (var _ in actual.WithCancellation(cts.Token))
        {
        }

        client.ListSessionsCalls.Should().ContainSingle().Which.Should().Be((filter, cts.Token));
    }

    [Fact]
    public async Task GetRateLimitsAsync_DelegatesToClient()
    {
        var expected = new RateLimits(null, null, null);
        var client = new FakeCodexClient { GetRateLimitsResult = expected };
        var facade = new CodexExecFacade(client);

        using var cts = new CancellationTokenSource();

        var actual = await facade.GetRateLimitsAsync(noCache: true, ct: cts.Token);

        actual.Should().BeSameAs(expected);
        client.GetRateLimitsCalls.Should().ContainSingle().Which.Should().Be((true, cts.Token));
    }

    [Fact]
    public async Task ReviewAsync_DelegatesToClient()
    {
        var expected = new CodexReviewResult(0, "ok", "");
        var client = new FakeCodexClient { ReviewResult = expected };
        var facade = new CodexExecFacade(client);

        using var cts = new CancellationTokenSource();
        var options = new CodexReviewOptions(@"C:\repo") { Prompt = "hi" };

        var actual = await facade.ReviewAsync(options, cts.Token);

        actual.Should().Be(expected);
        client.ReviewCalls.Should().ContainSingle().Which.Should().Be((options, cts.Token));
    }

    private sealed class FakeCodexClient : ICodexClient
    {
        public List<(CodexSessionOptions Options, CancellationToken Ct)> StartSessionCalls { get; } = new();
        public FakeSessionHandle? StartSessionResult { get; set; }

        public List<(SessionId SessionId, CodexSessionOptions Options, CancellationToken Ct)> ResumeWithOptionsCalls { get; } = new();
        public FakeSessionHandle? ResumeWithOptionsResult { get; set; }

        public List<(SessionId SessionId, CancellationToken Ct)> ResumeReadOnlyCalls { get; } = new();
        public FakeSessionHandle? ResumeReadOnlyResult { get; set; }

        public List<(string LogFilePath, CancellationToken Ct)> AttachToLogCalls { get; } = new();
        public FakeSessionHandle? AttachToLogResult { get; set; }

        public List<(SessionFilter? Filter, CancellationToken Ct)> ListSessionsCalls { get; } = new();
        public IAsyncEnumerable<CodexSessionInfo> ListSessionsResult { get; set; } = new TestAsyncEnumerable<CodexSessionInfo>();

        public List<(bool NoCache, CancellationToken Ct)> GetRateLimitsCalls { get; } = new();
        public RateLimits? GetRateLimitsResult { get; set; }

        public List<(CodexReviewOptions Options, CancellationToken Ct)> ReviewCalls { get; } = new();
        public CodexReviewResult? ReviewResult { get; set; }

        public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken)
        {
            StartSessionCalls.Add((options, cancellationToken));
            return Task.FromResult<ICodexSessionHandle>(StartSessionResult ?? new FakeSessionHandle());
        }

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CancellationToken cancellationToken)
        {
            ResumeWithOptionsCalls.Add((sessionId, options, cancellationToken));
            return Task.FromResult<ICodexSessionHandle>(ResumeWithOptionsResult ?? new FakeSessionHandle());
        }

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
        {
            ResumeReadOnlyCalls.Add((sessionId, cancellationToken));
            return Task.FromResult<ICodexSessionHandle>(ResumeReadOnlyResult ?? new FakeSessionHandle());
        }

        public Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken)
        {
            AttachToLogCalls.Add((logFilePath, cancellationToken));
            return Task.FromResult<ICodexSessionHandle>(AttachToLogResult ?? new FakeSessionHandle());
        }

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter, CancellationToken cancellationToken)
        {
            ListSessionsCalls.Add((filter, cancellationToken));
            return ListSessionsResult;
        }

        public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default)
        {
            GetRateLimitsCalls.Add((noCache, cancellationToken));
            return Task.FromResult(GetRateLimitsResult);
        }

        public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default)
        {
            ReviewCalls.Add((options, cancellationToken));
            return Task.FromResult(ReviewResult ?? new CodexReviewResult(0, "", ""));
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSessionHandle : ICodexSessionHandle
    {
        public CodexSessionInfo Info { get; } = new(SessionId.Parse("abc123"), @"C:\logs\abc.jsonl", DateTimeOffset.UtcNow);
        public SessionExitReason ExitReason => SessionExitReason.Unknown;
        public bool IsLive => false;

        public IAsyncEnumerable<CodexEvent> GetEventsAsync(EventStreamOptions? options, CancellationToken cancellationToken) =>
            new TestAsyncEnumerable<CodexEvent>();

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) =>
            Task.FromException<int>(new InvalidOperationException("Not live."));

        public Task<int> ExitAsync(CancellationToken cancellationToken) =>
            Task.FromException<int>(new InvalidOperationException("Not live."));

        public IDisposable OnExit(Action<int> callback) => new NoopDisposable();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class TestAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator();

        private sealed class TestAsyncEnumerator : IAsyncEnumerator<T>
        {
            public T Current => default!;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
        }
    }
}
