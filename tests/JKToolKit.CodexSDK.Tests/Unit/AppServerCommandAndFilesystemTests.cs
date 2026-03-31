using System.Text.Json;
using System.Text;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerCommandAndFilesystemTests
{
    [Fact]
    public async Task CommandExecAsync_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse("""{"exitCode":0,"stdout":"ok","stderr":""}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["git", "status"],
            Cwd = "C:\\repo",
            ProcessId = "proc-1",
            StreamStdoutStderr = true
        });

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("ok");
        rpc.LastMethod.Should().Be("command/exec");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"processId\":\"proc-1\"");
    }

    [Fact]
    public async Task CommandExecAsync_RejectsConflictingTimeoutOptions()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            DisableTimeout = true,
            TimeoutMs = 100
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*DisableTimeout*TimeoutMs*");
    }

    [Fact]
    public async Task CommandExecAsync_RequiresProcessId_WhenStreaming()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            StreamStdoutStderr = true
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ProcessId*required*");
    }

    [Fact]
    public async Task ThreadShellCommandAsync_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        await client.ThreadShellCommandAsync(new ThreadShellCommandOptions
        {
            ThreadId = "thr-1",
            Command = "git status"
        });

        rpc.LastMethod.Should().Be("thread/shellCommand");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"threadId\":\"thr-1\"")
            .And.Contain("\"command\":\"git status\"");
    }

    [Fact]
    public async Task UpdateThreadMetadataAsync_SendsPatchFlags_AndParsesThread()
    {
        using var doc = JsonDocument.Parse("""{"thread":{"id":"thr-1"}}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.UpdateThreadMetadataAsync(new ThreadMetadataUpdateOptions
        {
            ThreadId = "thr-1",
            GitInfo = new ThreadGitInfoUpdate
            {
                Branch = "main",
                UpdateBranch = true,
                UpdateSha = true
            }
        });

        result.Thread.Id.Should().Be("thr-1");
        rpc.LastMethod.Should().Be("thread/metadata/update");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"branch\":\"main\"")
            .And.Contain("\"sha\":null")
            .And.NotContain("originUrl");
    }

    [Fact]
    public async Task SetExperimentalFeatureEnablementAsync_ParsesResponse()
    {
        using var doc = JsonDocument.Parse("""{"enablement":{"featureA":true,"featureB":false}}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc, new CodexAppServerClientOptions { ExperimentalApi = true });

        var result = await client.SetExperimentalFeatureEnablementAsync(new ExperimentalFeatureEnablementSetOptions
        {
            Enablement = new Dictionary<string, bool> { ["featureA"] = true }
        });

        result.Enablement.Should().Contain(new KeyValuePair<string, bool>("featureA", true));
        result.Enablement.Should().Contain(new KeyValuePair<string, bool>("featureB", false));
        rpc.LastMethod.Should().Be("experimentalFeatureEnablement/set");
    }

    [Fact]
    public async Task FsWatchAsync_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse("""{"path":"C:\\repo","watchId":"watch-1"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.FsWatchAsync(new FsWatchOptions { Path = "C:\\repo" });

        result.WatchId.Should().Be("watch-1");
        result.Path.Should().Be("C:\\repo");
        rpc.LastMethod.Should().Be("fs/watch");
    }

    [Fact]
    public async Task FsReadWriteMetadataDirectoryCopyAndRemoveAsync_ParseExpectedResults()
    {
        await using var client = CreateClient(new SequencedRecordingRpc(
        [
            JsonDocument.Parse("""{"dataBase64":"aGVsbG8="}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement,
            JsonDocument.Parse("""{"isFile":"true","isDirectory":false,"createdAtMs":"123","modifiedAtMs":456}""").RootElement,
            JsonDocument.Parse("""{"entries":[{"file_name":"a.txt","isFile":"true","isDirectory":false}]}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement
        ]));

        var read = await client.FsReadFileAsync(new FsReadFileOptions { Path = "C:\\repo\\a.txt" });
        read.DataBase64.Should().Be("aGVsbG8=");
        Encoding.UTF8.GetString(Convert.FromBase64String(read.DataBase64)).Should().Be("hello");

        _ = await client.FsWriteFileAsync(new FsWriteFileOptions { Path = "C:\\repo\\a.txt", DataBase64 = "aGVsbG8=" });
        _ = await client.FsCreateDirectoryAsync(new FsCreateDirectoryOptions { Path = "C:\\repo\\dir", Recursive = true });

        var metadata = await client.FsGetMetadataAsync(new FsGetMetadataOptions { Path = "C:\\repo\\a.txt" });
        metadata.IsFile.Should().BeTrue();
        metadata.IsDirectory.Should().BeFalse();
        metadata.CreatedAtMs.Should().Be(123);
        metadata.ModifiedAtMs.Should().Be(456);

        var directory = await client.FsReadDirectoryAsync(new FsReadDirectoryOptions { Path = "C:\\repo" });
        directory.Entries.Should().ContainSingle();
        directory.Entries[0].FileName.Should().Be("a.txt");
        directory.Entries[0].IsFile.Should().BeTrue();

        _ = await client.FsCopyAsync(new FsCopyOptions { SourcePath = "C:\\repo\\a.txt", DestinationPath = "C:\\repo\\b.txt" });
        _ = await client.FsRemoveAsync(new FsRemoveOptions { Path = "C:\\repo\\b.txt", Force = true });
    }

    [Fact]
    public async Task FsUnwatchAsync_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        await client.FsUnwatchAsync(new FsUnwatchOptions { WatchId = "watch-1" });

        rpc.LastMethod.Should().Be("fs/unwatch");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"watchId\":\"watch-1\"");
    }

    private static CodexAppServerClient CreateClient(IJsonRpcConnection rpc, CodexAppServerClientOptions? options = null) =>
        new(
            options ?? new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => 0;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }
        public required JsonElement Result { get; init; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SequencedRecordingRpc : IJsonRpcConnection
    {
        private readonly Queue<JsonElement> _results;
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }

        public SequencedRecordingRpc(IEnumerable<JsonElement> results)
        {
            _results = new Queue<JsonElement>(results.Select(x => x.Clone()));
        }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(_results.Dequeue());
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
