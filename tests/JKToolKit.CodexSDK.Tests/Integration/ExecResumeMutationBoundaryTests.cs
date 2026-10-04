using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.StructuredOutputs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class ExecResumeMutationBoundaryTests
{
    [Fact]
    public async Task SuccessfulResume_TransfersSchemaOwnership_AndDisposalPublishesActualProcessCode()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Client();
        var options = fixture.Options();
        var handle = await client.ResumeSessionAsync(CodexResumeTarget.BySelector("friendly"), options);
        try
        {
            Assert.Equal("selected-id", fixture.LaunchedId!.Value.Value);
            Assert.Equal("friendly", fixture.LaunchedOptions!.ResumeTargetOverride!.Selector);
            Assert.Equal(CodexOutputSchemaKind.Json, options.OutputSchema!.Kind);
            Assert.True(File.Exists(fixture.SchemaPath));
            Assert.Equal(options.OutputSchema.Json!.Value.GetRawText(), File.ReadAllText(fixture.SchemaPath!));
            Assert.True(handle.IsLive);
            var callback = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = handle.OnExit(code => callback.TrySetResult(code));
            await handle.DisposeAsync();
            // The launcher deliberately returns 27 after the child exits with 9.
            Assert.Equal(9, await callback.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(SessionExitReason.Custom, handle.ExitReason);
            Assert.False(File.Exists(fixture.SchemaPath));
            Assert.Equal(1, fixture.TerminateCount);
            Assert.True(fixture.ProcessHandle.IsClosed);
        }
        finally { await handle.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedResume_DeletesSchemaAndStopsChild_EvenWhenGracefulTerminationFails(bool terminateFails)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.ValidationFailure = new IOException("selected log unavailable");
        fixture.TerminateFails = terminateFails;
        using var client = fixture.Client();
        var failure = await Assert.ThrowsAsync<IOException>(() => client.ResumeSessionAsync(CodexResumeTarget.BySelector("friendly"), fixture.Options()));
        Assert.Same(fixture.ValidationFailure, failure);
        await fixture.Observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fixture.TerminateCount);
        Assert.False(fixture.TerminationToken.CanBeCanceled);
        Assert.False(File.Exists(fixture.SchemaPath));
        Assert.True(fixture.ProcessHandle.IsClosed);
    }

    [Fact]
    public async Task CanceledResumeAfterLaunch_StillCleansUpWithUncancelableTerminationToken()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        fixture.OnValidate = cancellation.Cancel;
        using var client = fixture.Client();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ResumeSessionAsync(CodexResumeTarget.BySelector("friendly"), fixture.Options(), cancellation.Token));
        await fixture.Observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fixture.TerminateCount);
        Assert.False(fixture.TerminationToken.CanBeCanceled);
        Assert.False(File.Exists(fixture.SchemaPath));
        Assert.True(fixture.ProcessHandle.IsClosed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SilentResume_CaptureWaitIsBounded_AndUsesDifferentBudgetForSelectedSessions(bool selected)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.IncludeSelected = selected;
        using var client = fixture.Client(selected ? TimeSpan.FromSeconds(12) : TimeSpan.FromMilliseconds(600));
        // Child startup is completed before timing begins, including on Windows.
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(1400));
        var stopwatch = Stopwatch.StartNew();
        await using var handle = await client.ResumeSessionAsync(CodexResumeTarget.BySelector("friendly"), fixture.Options(), deadline.Token);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(selected ? 100 : 300), "Resume should give the silent child its session-id capture budget before falling back.");
        Assert.Equal("selected-id", handle.Info.Id.Value);
        Assert.Equal(selected ? "selected-id" : "friendly", fixture.LaunchedId!.Value.Value);
    }

    private sealed class Fixture : ICodexProcessLauncher, ICodexSessionLocator, ICodexPathProvider, IAsyncDisposable
    {
        private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "codex-resume-boundary-" + Guid.NewGuid().ToString("N"))).FullName;
        private Process _process = null!;
        public Process Observer { get; private set; } = null!;
        public SafeProcessHandle ProcessHandle { get; private set; } = null!;
        public bool IncludeSelected = true;
        public bool TerminateFails;
        public int TerminateCount;
        public CancellationToken TerminationToken;
        public Exception? ValidationFailure;
        public Action? OnValidate;
        public SessionId? LaunchedId;
        public CodexSessionOptions? LaunchedOptions;
        public string? SchemaPath => LaunchedOptions?.OutputSchema?.FilePath;
        private string LogPath => Path.Combine(_root, "log.jsonl");

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            File.WriteAllText(fixture.LogPath, """{"timestamp":"2026-04-01T12:00:00Z","type":"session_meta","payload":{"id":"selected-id"}}""" + Environment.NewLine);
            var start = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            if (OperatingSystem.IsWindows()) start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add(OperatingSystem.IsWindows() ? "-Command" : "-c");
            start.ArgumentList.Add(OperatingSystem.IsWindows()
                ? "[Console]::WriteLine('ready'); $code = [Console]::ReadLine(); if ($null -eq $code) { Start-Sleep -Seconds 30 } else { exit ([int]$code) }"
                : "echo ready; if read code; then exit \"$code\"; fi; sleep 30");
            fixture._process = Process.Start(start)!;
            fixture.Observer = Process.GetProcessById(fixture._process.Id);
            fixture.ProcessHandle = fixture._process.SafeHandle;
            try
            {
                Assert.Equal("ready", await fixture._process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)));
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public CodexSessionOptions Options() => new(_root, "resume")
        {
            OutputSchema = CodexOutputSchema.FromJson(JsonSerializer.SerializeToElement(new { type = "object", title = "test schema" }))
        };

        public CodexClient Client(TimeSpan? startTimeout = null) => new(
            new CodexClientOptions { StartTimeout = startTimeout ?? TimeSpan.FromSeconds(4), SessionsRootDirectory = _root, EnableUncorrelatedNewSessionFileDiscovery = true },
            this, this, new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, Microsoft.Extensions.Options.Options.Create(new CodexClientOptions())),
            new JsonlEventParser(NullLogger<JsonlEventParser>.Instance), this);

        public Task<Process> ResumeSessionAsync(SessionId id, CodexSessionOptions options, CodexClientOptions client, CancellationToken ct)
        {
            LaunchedId = id;
            LaunchedOptions = options;
            File.AppendAllText(LogPath, """{"timestamp":"2026-04-01T12:00:01Z","type":"agent_message","payload":{"message":"resumed"}}""" + Environment.NewLine);
            return Task.FromResult(_process);
        }
        public async Task<int> TerminateProcessAsync(Process process, TimeSpan timeout, CancellationToken ct)
        {
            TerminateCount++;
            TerminationToken = ct;
            if (TerminateFails) throw new IOException("graceful termination failed");
            await process.StandardInput.WriteLineAsync("9");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(5));
            return 27;
        }
        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<Process> StartReviewAsync(CodexReviewOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> ValidateLogFileAsync(string path, CancellationToken ct)
        {
            OnValidate?.Invoke();
            return ValidationFailure is null ? Task.FromResult(path) : Task.FromException<string>(ValidationFailure);
        }
        public Task<string> WaitForNewSessionFileAsync(string root, DateTimeOffset start, TimeSpan timeout, CancellationToken ct) => Task.FromResult(LogPath);
        public Task<string> FindSessionLogAsync(SessionId id, string root, CancellationToken ct) => Task.FromResult(LogPath);
        public Task<string> WaitForSessionLogByIdAsync(SessionId id, string root, TimeSpan timeout, CancellationToken ct) => Task.FromResult(LogPath);
        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string root, SessionFilter? filter, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            if (IncludeSelected) yield return new(SessionId.Parse("selected-id"), LogPath, DateTimeOffset.UnixEpoch, HumanLabel: "friendly");
        }
        public string GetSessionsRootDirectory(string? value) => _root;
        public string GetCodexExecutablePath(string? value) => throw new NotSupportedException();
        public string ResolveSessionLogPath(SessionId id, string? root) => throw new NotSupportedException();
        public async ValueTask DisposeAsync()
        {
            if (Observer is not null)
            {
                if (!Observer.HasExited) { Observer.Kill(true); await Observer.WaitForExitAsync(); }
                Observer.Dispose();
            }
            _process?.Dispose();
            if (SchemaPath is { } schema) File.Delete(schema);
            Directory.Delete(_root, true);
        }
    }
}
