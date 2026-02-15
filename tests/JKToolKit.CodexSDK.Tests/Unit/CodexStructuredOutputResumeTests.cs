using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.StructuredOutputs;
using FluentAssertions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexStructuredOutputResumeTests
{
    private sealed record MyDto(string Answer);

    [Fact]
    public async Task RunStructuredAsync_Resume_UsesTimestampFiltering()
    {
        var fake = new FakeCodexClient();
        var sessionId = SessionId.Parse("session-1");
        var options = new CodexSessionOptions(Path.GetTempPath(), "retry prompt");

        var result = await fake.RunStructuredAsync<MyDto>(sessionId, options);

        result.Value.Answer.Should().Be("ok");

        fake.ResumeCalls.Should().HaveCount(1);
        fake.ResumeCalls[0].Options.OutputSchema.Should().NotBeNull();

        fake.Handle.LastEventStreamOptions.Should().NotBeNull();
        fake.Handle.LastEventStreamOptions!.FromBeginning.Should().BeFalse();
        fake.Handle.LastEventStreamOptions!.AfterTimestamp.Should().NotBeNull();
    }

    private sealed class FakeCodexClient : ICodexClient
    {
        public sealed class ResumeCall
        {
            public required SessionId SessionId { get; init; }
            public required CodexSessionOptions Options { get; init; }
        }

        public List<ResumeCall> ResumeCalls { get; } = new();
        public FakeSessionHandle Handle { get; } = new();

        public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CancellationToken cancellationToken = default)
        {
            ResumeCalls.Add(new ResumeCall { SessionId = sessionId, Options = options });
            return Task.FromResult<ICodexSessionHandle>(Handle);
        }

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSessionHandle : ICodexSessionHandle
    {
        public EventStreamOptions? LastEventStreamOptions { get; private set; }

        public CodexSessionInfo Info { get; } = new(SessionId.Parse("session-1"), "log.jsonl", DateTimeOffset.UtcNow, "/tmp", null);
        public SessionExitReason ExitReason => SessionExitReason.Unknown;
        public bool IsLive => true;

        public IAsyncEnumerable<CodexEvent> GetEventsAsync(EventStreamOptions? options, CancellationToken cancellationToken)
        {
            LastEventStreamOptions = options;
            return Events();

            static async IAsyncEnumerable<CodexEvent> Events()
            {
                yield return new AgentMessageEvent { Type = "agent_message", Timestamp = DateTimeOffset.UtcNow, RawPayload = JsonDocument.Parse("{}").RootElement, Text = "{\"answer\":\"ok\"}" };
                yield return new TaskCompleteEvent { Type = "task_complete", Timestamp = DateTimeOffset.UtcNow, RawPayload = JsonDocument.Parse("{}").RootElement, LastAgentMessage = "{\"answer\":\"ok\"}" };
                await Task.CompletedTask;
            }
        }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> ExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public IDisposable OnExit(Action<int> callback) => new Noop();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

