using System.Text.Json;
using System.Text;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Tests.TestHelpers;
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
            Cwd = XPaths.Abs("repo"),
            ProcessId = "proc-1",
            StreamStdoutStderr = true,
            Tty = true,
            Size = new CommandExecTerminalSize
            {
                Columns = 101,
                Rows = 31
            }
        });

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("ok");
        rpc.LastMethod.Should().Be("command/exec");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"processId\":\"proc-1\"")
            .And.Contain("\"size\":{\"cols\":101,\"rows\":31}");
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
    public async Task CommandExecAsync_RejectsNegativeTimeoutMs()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            TimeoutMs = -1
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*TimeoutMs*cannot be negative*");
    }

    [Fact]
    public async Task CommandExecAsync_RejectsNegativeOutputBytesCap()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            OutputBytesCap = -1
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*OutputBytesCap*cannot be negative*");
    }

    [Fact]
    public async Task CommandExecAsync_SerializesLargeOutputBytesCap()
    {
        using var doc = JsonDocument.Parse("""{"exitCode":0,"stdout":"","stderr":""}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        _ = await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            OutputBytesCap = 3_000_000_000
        });

        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"outputBytesCap\":3000000000");
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
    public async Task CommandExecAsync_RejectsZeroSizedTerminal()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            ProcessId = "proc-1",
            Tty = true,
            Size = new CommandExecTerminalSize
            {
                Columns = 0,
                Rows = 24
            }
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*rows and cols must be greater than 0*");
    }

    [Fact]
    public async Task CommandExecWriteAsync_RequiresPayloadOrCloseStdin()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecWriteAsync(new CommandExecWriteOptions
        {
            ProcessId = "proc-1"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*deltaBase64 or closeStdin*");
    }

    [Fact]
    public async Task CommandExecResizeAsync_RejectsZeroSizedTerminal()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecResizeAsync(new CommandExecResizeOptions
        {
            ProcessId = "proc-1",
            Size = new CommandExecTerminalSize
            {
                Columns = 80,
                Rows = 0
            }
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*rows and cols must be greater than 0*");
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
    public async Task ThreadShellCommandAsync_RequiresObjectResponse()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("null").RootElement };

        await using var client = CreateClient(rpc);

        var act = async () => await client.ThreadShellCommandAsync(new ThreadShellCommandOptions
        {
            ThreadId = "thr-1",
            Command = "git status"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*thread/shellCommand response*JSON object*");
    }

    [Fact]
    public async Task UpdateThreadMetadataAsync_SendsPatchFlags_AndParsesThread()
    {
        using var doc = JsonDocument.Parse("""{"thread":{"id":"thr-1","gitInfo":{"branch":"main","originUrl":"https://example.invalid/repo.git","sha":"abc123"}}}""");
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
        result.Thread.Thread.GitInfo.Should().NotBeNull();
        result.Thread.Thread.GitInfo!.Branch.Should().Be("main");
        result.Thread.Thread.GitInfo.OriginUrl.Should().Be("https://example.invalid/repo.git");
        result.Thread.Thread.GitInfo.Sha.Should().Be("abc123");
        rpc.LastMethod.Should().Be("thread/metadata/update");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"branch\":\"main\"")
            .And.Contain("\"sha\":null")
            .And.NotContain("originUrl");
    }

    [Fact]
    public async Task UpdateThreadMetadataAsync_RequiresGitInfo()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.UpdateThreadMetadataAsync(new ThreadMetadataUpdateOptions
        {
            ThreadId = "thr-1"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*GitInfo is required*");
    }

    [Fact]
    public async Task UpdateThreadMetadataAsync_RequiresAtLeastOnePatchFlag()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.UpdateThreadMetadataAsync(new ThreadMetadataUpdateOptions
        {
            ThreadId = "thr-1",
            GitInfo = new ThreadGitInfoUpdate()
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one GitInfo update flag must be set*");
    }

    [Fact]
    public async Task UpdateThreadMetadataAsync_RejectsWhitespacePatchValues()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.UpdateThreadMetadataAsync(new ThreadMetadataUpdateOptions
        {
            ThreadId = "thr-1",
            GitInfo = new ThreadGitInfoUpdate
            {
                UpdateBranch = true,
                Branch = "   "
            }
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Branch cannot be empty or whitespace*");
    }

    [Fact]
    public async Task SetExperimentalFeatureEnablementAsync_ParsesResponse()
    {
        using var doc = JsonDocument.Parse("""{"enablement":{"featureA":true,"featureB":false}}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.SetExperimentalFeatureEnablementAsync(new ExperimentalFeatureEnablementSetOptions
        {
            Enablement = new Dictionary<string, bool> { ["featureA"] = true }
        });

        result.Enablement.Should().Contain(new KeyValuePair<string, bool>("featureA", true));
        result.Enablement.Should().Contain(new KeyValuePair<string, bool>("featureB", false));
        rpc.LastMethod.Should().Be("experimentalFeature/enablement/set");
    }

    [Fact]
    public async Task SetExperimentalFeatureEnablementAsync_InvalidResponse_Throws()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{"enablement":{"featureA":"yes"}}""").RootElement };

        await using var client = CreateClient(rpc);

        var act = async () => await client.SetExperimentalFeatureEnablementAsync(new ExperimentalFeatureEnablementSetOptions
        {
            Enablement = new Dictionary<string, bool> { ["featureA"] = true }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-boolean enablement value*");
    }

    [Fact]
    public async Task FsWatchAsync_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse($"{{\"path\":\"{XPaths.JsonEsc("repo")}\"}}");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.FsWatchAsync(new FsWatchOptions { Path = XPaths.Abs("repo"), WatchId = "watch-1" });

        result.WatchId.Should().Be("watch-1");
        result.Path.Should().Be(XPaths.Abs("repo"));
        rpc.LastMethod.Should().Be("fs/watch");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"watchId\":\"watch-1\"");
    }

    [Fact]
    public async Task FsWatchAsync_WhenWatchIdIsOmitted_GeneratesStableResultId()
    {
        using var doc = JsonDocument.Parse($"{{\"path\":\"{XPaths.JsonEsc("repo")}\"}}");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.FsWatchAsync(new FsWatchOptions { Path = XPaths.Abs("repo") });

        result.WatchId.Should().StartWith("watch_");
        var payload = JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        payload.Should().Contain($"\"watchId\":\"{result.WatchId}\"");
    }

    [Fact]
    public async Task FsReadFileAsync_RequiresAbsolutePath()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{"dataBase64":"aGVsbG8="}""").RootElement });

        var act = async () => await client.FsReadFileAsync(new FsReadFileOptions { Path = "relative\\file.txt" });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute*");
    }

    [Fact]
    public async Task FsReadFileAsync_RequiresDataBase64()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.FsReadFileAsync(new FsReadFileOptions { Path = XPaths.Abs("repo/a.txt") });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fs/readFile response*");
    }

    [Fact]
    public async Task FsWriteFileAsync_AllowsEmptyPayload()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        _ = await client.FsWriteFileAsync(new FsWriteFileOptions
        {
            Path = XPaths.Abs("repo/empty.txt"),
            DataBase64 = string.Empty
        });

        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"dataBase64\":\"\"");
    }

    [Fact]
    public async Task FsReadWriteMetadataDirectoryCopyAndRemoveAsync_ParseExpectedResults()
    {
        await using var client = CreateClient(new SequencedRecordingRpc(
        [
            JsonDocument.Parse("""{"dataBase64":"aGVsbG8="}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement,
            JsonDocument.Parse("""{"isFile":true,"isDirectory":false,"createdAtMs":123,"modifiedAtMs":456}""").RootElement,
            JsonDocument.Parse("""{"entries":[{"fileName":"a.txt","isFile":true,"isDirectory":false}]}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement,
            JsonDocument.Parse("""{}""").RootElement
        ]));

        var read = await client.FsReadFileAsync(new FsReadFileOptions { Path = XPaths.Abs("repo/a.txt") });
        read.DataBase64.Should().Be("aGVsbG8=");
        Encoding.UTF8.GetString(Convert.FromBase64String(read.DataBase64)).Should().Be("hello");

        _ = await client.FsWriteFileAsync(new FsWriteFileOptions { Path = XPaths.Abs("repo/a.txt"), DataBase64 = "aGVsbG8=" });
        _ = await client.FsCreateDirectoryAsync(new FsCreateDirectoryOptions { Path = XPaths.Abs("repo/dir"), Recursive = true });

        var metadata = await client.FsGetMetadataAsync(new FsGetMetadataOptions { Path = XPaths.Abs("repo/a.txt") });
        metadata.IsFile.Should().BeTrue();
        metadata.IsDirectory.Should().BeFalse();
        metadata.CreatedAtMs.Should().Be(123);
        metadata.ModifiedAtMs.Should().Be(456);

        var directory = await client.FsReadDirectoryAsync(new FsReadDirectoryOptions { Path = XPaths.Abs("repo") });
        directory.Entries.Should().ContainSingle();
        directory.Entries[0].FileName.Should().Be("a.txt");
        directory.Entries[0].IsFile.Should().BeTrue();

        _ = await client.FsCopyAsync(new FsCopyOptions { SourcePath = XPaths.Abs("repo/a.txt"), DestinationPath = XPaths.Abs("repo/b.txt") });
        _ = await client.FsRemoveAsync(new FsRemoveOptions { Path = XPaths.Abs("repo/b.txt"), Force = true });
    }

    [Fact]
    public async Task FsGetMetadataAsync_StringTimestamps_AreRejected()
    {
        using var doc = JsonDocument.Parse("""{"isFile":true,"isDirectory":false,"createdAtMs":"123","modifiedAtMs":"456"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var act = async () => await client.FsGetMetadataAsync(new FsGetMetadataOptions { Path = XPaths.Abs("repo/a.txt") });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*createdAtMs*");
    }

    [Fact]
    public async Task FsReadDirectoryAsync_NonObjectEntries_AreRejected()
    {
        using var doc = JsonDocument.Parse("""{"entries":[123]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var act = async () => await client.FsReadDirectoryAsync(new FsReadDirectoryOptions { Path = XPaths.Abs("repo") });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entries[]*objects*");
    }

    [Fact]
    public async Task CommandExecAsync_StringExitCode_IsRejected()
    {
        using var doc = JsonDocument.Parse("""{"exitCode":"0","stdout":"","stderr":""}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"]
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exitCode*");
    }

    [Theory]
    [InlineData("fs/writeFile")]
    [InlineData("fs/createDirectory")]
    [InlineData("fs/remove")]
    [InlineData("fs/copy")]
    [InlineData("fs/unwatch")]
    [InlineData("command/exec/write")]
    [InlineData("command/exec/resize")]
    [InlineData("command/exec/terminate")]
    public async Task EmptySuccessResponses_RequireObjectPayloads(string method)
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("null").RootElement };
        await using var client = CreateClient(rpc);

        Func<Task> act = method switch
        {
            "fs/writeFile" => () => client.FsWriteFileAsync(new FsWriteFileOptions { Path = XPaths.Abs("repo/a.txt"), DataBase64 = string.Empty }),
            "fs/createDirectory" => () => client.FsCreateDirectoryAsync(new FsCreateDirectoryOptions { Path = XPaths.Abs("repo/dir") }),
            "fs/remove" => () => client.FsRemoveAsync(new FsRemoveOptions { Path = XPaths.Abs("repo/a.txt") }),
            "fs/copy" => () => client.FsCopyAsync(new FsCopyOptions { SourcePath = XPaths.Abs("repo/a.txt"), DestinationPath = XPaths.Abs("repo/b.txt") }),
            "fs/unwatch" => () => client.FsUnwatchAsync(new FsUnwatchOptions { WatchId = "watch-1" }),
            "command/exec/write" => () => client.CommandExecWriteAsync(new CommandExecWriteOptions { ProcessId = "proc-1", DeltaBase64 = string.Empty }),
            "command/exec/resize" => () => client.CommandExecResizeAsync(new CommandExecResizeOptions
            {
                ProcessId = "proc-1",
                Size = new CommandExecTerminalSize { Columns = 80, Rows = 24 }
            }),
            "command/exec/terminate" => () => client.CommandExecTerminateAsync(new CommandExecTerminateOptions { ProcessId = "proc-1" }),
            _ => throw new NotSupportedException(method)
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JSON object*");
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
