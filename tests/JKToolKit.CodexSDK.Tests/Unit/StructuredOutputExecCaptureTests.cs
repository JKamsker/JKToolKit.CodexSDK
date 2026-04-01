using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.StructuredOutputs.Internal;
using FluentAssertions;
using System.Text.Json;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StructuredOutputExecCaptureTests
{
    [Fact]
    public async Task CaptureExecFinalTextAsync_FallsBackToResponseItemAssistantMessage_WhenNoAgentMessageOrTaskComplete()
    {
        var json = "{\"answer\":\"ok\"}";
        var session = new FakeSessionHandle(isLive: false, events: new CodexEvent[]
        {
            CreateResponseItemAssistantMessage(json)
        });

        var raw = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(
            session,
            EventStreamOptions.Default,
            ct: CancellationToken.None);

        raw.Should().Be(json);
    }

    [Fact]
    public async Task CaptureExecFinalTextAsync_FallsBackToResponseItemAssistantMessage_WhenTaskCompleteLastAgentMessageIsNull()
    {
        var json = "{\"answer\":\"ok\"}";
        var session = new FakeSessionHandle(isLive: false, events: new CodexEvent[]
        {
            new TaskCompleteEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = "task_complete",
                RawPayload = CreateEmptyPayload(),
                LastAgentMessage = null
            },
            CreateResponseItemAssistantMessage(json)
        });

        var raw = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(
            session,
            EventStreamOptions.Default,
            ct: CancellationToken.None);

        raw.Should().Be(json);
    }

    [Fact]
    public async Task CaptureExecFinalTextAsync_FallsBackToTurnItemCompletedText_WhenItLooksLikeJson()
    {
        var json = "{\"answer\":\"ok\"}";
        var session = new FakeSessionHandle(isLive: false, events: new CodexEvent[]
        {
            new TurnItemCompletedEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = "item_completed",
                RawPayload = CreateEmptyPayload(),
                ItemType = "plan",
                Text = json
            }
        });

        var raw = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(
            session,
            EventStreamOptions.Default,
            ct: CancellationToken.None);

        raw.Should().Be(json);
    }

    [Fact]
    public async Task CaptureExecFinalTextAsync_RejectsLiveSession_WhenProcessExitsNonZero()
    {
        var session = new FakeSessionHandle(
            isLive: true,
            exitCode: 2,
            events: new CodexEvent[]
            {
                CreateResponseItemAssistantMessage("{\"answer\":\"stale\"}")
            });

        var act = async () => await StructuredOutputExecCapture.CaptureExecFinalTextAsync(
            session,
            EventStreamOptions.Default,
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<JKToolKit.CodexSDK.StructuredOutputs.CodexStructuredOutputParseException>()
            .WithMessage("*exited with code 2*");
    }

    private static ResponseItemEvent CreateResponseItemAssistantMessage(string text) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = "response_item",
            RawPayload = CreateEmptyPayload(),
            PayloadType = "message",
            Payload = new MessageResponseItemPayload
            {
                PayloadType = "message",
                Role = "assistant",
                Content = new ResponseMessageContentPart[]
                {
                    new ResponseMessageOutputTextPart
                    {
                        ContentType = "output_text",
                        Text = text
                    }
                }
            }
        };

    private static JsonElement CreateEmptyPayload()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private sealed class FakeSessionHandle : ICodexSessionHandle
    {
        private readonly IReadOnlyList<CodexEvent> _events;
        private readonly int _exitCode;

        public FakeSessionHandle(bool isLive, IReadOnlyList<CodexEvent> events, int exitCode = 0)
        {
            _events = events;
            _exitCode = exitCode;
            IsLive = isLive;
        }

        public CodexSessionInfo Info { get; } = new(SessionId.Parse("session-1"), "log.jsonl", DateTimeOffset.UtcNow, "/tmp", null);
        public SessionExitReason ExitReason => SessionExitReason.Unknown;
        public bool IsLive { get; }

        public IAsyncEnumerable<CodexEvent> GetEventsAsync(EventStreamOptions? options, CancellationToken cancellationToken) =>
            _events.ToAsyncEnumerable();

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(_exitCode);
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
