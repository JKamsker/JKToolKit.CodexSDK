using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerClientGuardrailSeamTests
{
    [Fact]
    public async Task StartThread_WithDynamicTools_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var tool = new DynamicToolSpec
        {
            Name = "echo",
            Description = "Echoes the input.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
        };

        var act = async () => await client.StartThreadAsync(new ThreadStartOptions
        {
            DynamicTools = new[] { tool }
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task StartThread_WithPersistExtendedHistory_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.StartThreadAsync(new ThreadStartOptions
        {
            PersistExtendedHistory = true
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeThread_WithPersistExtendedHistory_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            ThreadId = "thr_1",
            PersistExtendedHistory = true
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ForkThread_WithPersistExtendedHistory_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.ForkThreadAsync(new ThreadForkOptions
        {
            ThreadId = "thr_1",
            PersistExtendedHistory = true
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeThread_WithExperimentalPath_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            Path = "/tmp/rollout"
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task UnsubscribeThread_WhenExperimentalDisabled_SendsThreadUnsubscribe()
    {
        using var doc = JsonDocument.Parse("""{"status":"unsubscribed"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.UnsubscribeThreadAsync("thr_1");

        result.Status.Should().Be(ThreadUnsubscribeStatus.Unsubscribed);
        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/unsubscribe");

        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"threadId\":\"thr_1\"");
    }

    [Fact]
    public async Task ResumeThread_WithPathOnly_AllowsNullThreadId_WhenExperimentalEnabled()
    {
        using var doc = JsonDocument.Parse("""{"threadId":"thr_1"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var thread = await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            Path = "/tmp/rollout"
        });

        thread.Id.Should().Be("thr_1");
        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/resume");
        rpc.LastParams.Should().BeOfType<ThreadResumeParams>();
        ((ThreadResumeParams)rpc.LastParams!).ThreadId.Should().BeNull();
    }

    [Fact]
    public async Task StartThread_WithDynamicTools_WhenExperimentalEnabled_SendsThreadStart()
    {
        using var doc = JsonDocument.Parse("""{"threadId":"thr_1"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var tool = new DynamicToolSpec
        {
            Name = "echo",
            Description = "Echoes the input.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
        };

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            DynamicTools = new[] { tool },
            PersistExtendedHistory = true
        });

        thread.Id.Should().Be("thr_1");
        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/start");
        rpc.LastParams.Should().BeOfType<ThreadStartParams>();

        var p = (ThreadStartParams)rpc.LastParams!;
        p.DynamicTools.Should().BeEquivalentTo(new[] { tool });
        p.PersistExtendedHistory.Should().BeTrue();
    }

    [Fact]
    public async Task StartThreadRealtime_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.StartThreadRealtimeAsync("thr_1", "hello", sessionId: "sess_1");

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task AppendThreadRealtimeAudio_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var audio = new ThreadRealtimeAudioChunk
        {
            Data = "AA==",
            NumChannels = 1,
            SampleRate = 16000,
            SamplesPerChannel = 160
        };

        var act = async () => await client.AppendThreadRealtimeAudioAsync("thr_1", audio);

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task AppendThreadRealtimeText_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.AppendThreadRealtimeTextAsync("thr_1", "hello");

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task StopThreadRealtime_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.StopThreadRealtimeAsync("thr_1");

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task StartThreadRealtime_WhenExperimentalEnabled_SendsThreadRealtimeStart()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        await client.StartThreadRealtimeAsync("thr_1", "hello", sessionId: "sess_1");

        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/realtime/start");
        rpc.LastParams.Should().BeOfType<ThreadRealtimeStartParams>();

        var p = (ThreadRealtimeStartParams)rpc.LastParams!;
        p.ThreadId.Should().Be("thr_1");
        p.Prompt.Should().Be("hello");
        p.SessionId.Should().Be("sess_1");
    }

    [Fact]
    public async Task ListCollaborationModes_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.ListCollaborationModesAsync();

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ListCollaborationModes_WhenExperimentalEnabled_SendsCollaborationModeList()
    {
        using var doc = JsonDocument.Parse("""{"data":[{"name":"default","mode":"plan","model":"gpt","reasoning_effort":"low"}]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.ListCollaborationModesAsync();

        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("collaborationMode/list");

        result.Data.Should().ContainSingle();
        result.Data[0].Name.Should().Be("default");
        result.Data[0].Mode.Should().Be("plan");
        result.Data[0].Model.Should().Be("gpt");
        result.Data[0].ReasoningEffort.Should().Be("low");
    }

    [Fact]
    public async Task AppendThreadRealtimeAudio_WhenExperimentalEnabled_SendsThreadRealtimeAppendAudio()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var audio = new ThreadRealtimeAudioChunk
        {
            Data = "AA==",
            NumChannels = 1,
            SampleRate = 16000,
            SamplesPerChannel = 160
        };

        await client.AppendThreadRealtimeAudioAsync("thr_1", audio);

        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/realtime/appendAudio");
        rpc.LastParams.Should().BeOfType<ThreadRealtimeAppendAudioParams>();

        var p = (ThreadRealtimeAppendAudioParams)rpc.LastParams!;
        p.ThreadId.Should().Be("thr_1");
        p.Audio.Should().BeEquivalentTo(audio);
    }

    [Fact]
    public async Task AppendThreadRealtimeText_WhenExperimentalEnabled_SendsThreadRealtimeAppendText()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        await client.AppendThreadRealtimeTextAsync("thr_1", "hello");

        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/realtime/appendText");
        rpc.LastParams.Should().BeOfType<ThreadRealtimeAppendTextParams>();

        var p = (ThreadRealtimeAppendTextParams)rpc.LastParams!;
        p.ThreadId.Should().Be("thr_1");
        p.Text.Should().Be("hello");
    }

    [Fact]
    public async Task StopThreadRealtime_WhenExperimentalEnabled_SendsThreadRealtimeStop()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        await client.StopThreadRealtimeAsync("thr_1");

        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/realtime/stop");
        rpc.LastParams.Should().BeOfType<ThreadRealtimeStopParams>();

        var p = (ThreadRealtimeStopParams)rpc.LastParams!;
        p.ThreadId.Should().Be("thr_1");
    }

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public int RequestCount { get; private set; }

#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            throw new InvalidOperationException("SendRequestAsync should not be called for guardrail failures.");
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        // ReSharper disable once UnusedMember.Local
        public ValueTask RaiseAsync(JsonRpcNotification notification) =>
            OnNotification?.Invoke(notification) ?? ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public int RequestCount { get; private set; }
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }

#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public required JsonElement Result { get; init; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
