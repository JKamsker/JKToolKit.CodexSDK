using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;
using FluentAssertions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexStructuredOutputRetryTests
{
    private sealed record MyDto(string Answer);

    [Fact]
    public async Task RunStructuredWithRetryAsync_RetriesViaResume_AndEventuallySucceeds()
    {
        var workingDirectory = Directory.CreateTempSubdirectory("codexsdk-retry").FullName;
        try
        {
            var client = new FakeCodexClient(
                start: FakeSessionHandle.Create("session-1", "log.jsonl", lastAgentMessage: "not json"),
                resume: FakeSessionHandle.Create("session-1", "log.jsonl", lastAgentMessage: "{\"answer\":\"ok\"}"));

            var options = new CodexSessionOptions(workingDirectory, "Return JSON.") { Model = CodexModel.Gpt52Codex };

            var result = await client.RunStructuredWithRetryAsync<MyDto>(options);

            result.Value.Answer.Should().Be("ok");
            client.StartCalls.Should().Be(1);
            client.ResumeCalls.Should().Be(1);
            client.LastResumePrompt.Should().NotBeNullOrWhiteSpace();
            client.LastResumePrompt.Should().Contain("Return ONLY valid JSON");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private sealed class FakeCodexClient : ICodexClient
    {
        private readonly FakeSessionHandle _start;
        private readonly FakeSessionHandle _resume;

        public int StartCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public string? LastResumePrompt { get; private set; }

        public FakeCodexClient(FakeSessionHandle start, FakeSessionHandle resume)
        {
            _start = start;
            _resume = resume;
        }

        public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.FromResult<ICodexSessionHandle>(_start);
        }

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CancellationToken cancellationToken = default)
        {
            ResumeCalls++;
            LastResumePrompt = options.Prompt;
            return Task.FromResult<ICodexSessionHandle>(_resume);
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
        private readonly string _lastAgentMessage;

        private FakeSessionHandle(CodexSessionInfo info, string lastAgentMessage)
        {
            Info = info;
            _lastAgentMessage = lastAgentMessage;
        }

        public static FakeSessionHandle Create(string sessionId, string logPath, string lastAgentMessage) =>
            new(new CodexSessionInfo(SessionId.Parse(sessionId), logPath, DateTimeOffset.UtcNow, "/tmp", null), lastAgentMessage);

        public CodexSessionInfo Info { get; }
        public SessionExitReason ExitReason => SessionExitReason.Unknown;
        public bool IsLive => true;

        public IAsyncEnumerable<CodexEvent> GetEventsAsync(EventStreamOptions? options, CancellationToken cancellationToken) => Events();

        private async IAsyncEnumerable<CodexEvent> Events()
        {
            using var rawDoc = JsonDocument.Parse("{\"type\":\"event_msg\",\"payload\":{}}");
            yield return new AgentMessageEvent { Type = "agent_message", Timestamp = DateTimeOffset.UtcNow, RawPayload = rawDoc.RootElement.Clone(), Text = _lastAgentMessage };
            yield return new TaskCompleteEvent { Type = "task_complete", Timestamp = DateTimeOffset.UtcNow, RawPayload = rawDoc.RootElement.Clone(), LastAgentMessage = _lastAgentMessage };
            await Task.CompletedTask;
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
