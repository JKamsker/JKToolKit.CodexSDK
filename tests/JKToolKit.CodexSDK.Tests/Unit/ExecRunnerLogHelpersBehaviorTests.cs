using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.StructuredOutputs;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecRunnerLogHelpersBehaviorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CapturedIdLookupFailure_FallsBackToSelectedOrDiscoveredLog(bool selected, bool timeout)
    {
        var fixture = new Fixture { LookupFailure = timeout ? new OperationCanceledException() : new IOException("missing") };
        var result = await CodexSessionRunnerLogHelpers.ResolveResumeLogPathAsync(fixture,
            selected ? ExecEventPipelineBehaviorTests.Info : null, SessionId.Parse("captured"), "root", Task.FromResult("discovered"),
            TimeSpan.FromSeconds(1), CodexResumeTarget.MostRecent(), NullLogger.Instance, default);
        Assert.Equal(selected ? "validated:log.jsonl" : "discovered", result);
        Assert.Equal(1, fixture.LookupCalls);
        Assert.Equal(selected ? 1 : 0, fixture.ValidateCalls);
    }

    [Fact]
    public async Task CapturedIdLookupCancellation_DoesNotUseFallbackWhenCallerCanceled()
    {
        var fixture = new Fixture { LookupFailure = new OperationCanceledException() };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CodexSessionRunnerLogHelpers.ResolveResumeLogPathAsync(fixture,
            ExecEventPipelineBehaviorTests.Info, SessionId.Parse("captured"), "root", Task.FromResult("discovered"), TimeSpan.FromSeconds(1),
            CodexResumeTarget.MostRecent(), NullLogger.Instance, canceled.Token));
        Assert.Equal(0, fixture.ValidateCalls);
    }

    [Fact]
    public async Task MissingResumeTargetWithoutFallback_HasUsefulFailure_AndLookupErrorsArePreserved()
    {
        var fixture = new Fixture();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => CodexSessionRunnerLogHelpers.ResolveResumeLogPathAsync(fixture,
            null, null, "root", null, TimeSpan.FromSeconds(1), CodexResumeTarget.MostRecent(true), NullLogger.Instance, default));
        Assert.Contains("--last --all", failure.Message);
        fixture.LookupFailure = new IOException("lookup failed");
        Assert.Same(fixture.LookupFailure, await Assert.ThrowsAsync<IOException>(() => CodexSessionRunnerLogHelpers.ResolveResumeLogPathAsync(fixture,
            null, SessionId.Parse("captured"), "root", null, TimeSpan.FromSeconds(1), CodexResumeTarget.MostRecent(), NullLogger.Instance, default)));
    }

    [Fact]
    public async Task FallbackDiscovery_OnlyStartsWhenEnabledAndUnselected_AndWrapsSynchronousFailure()
    {
        var fixture = new Fixture();
        var options = new CodexClientOptions();
        Assert.Null(CodexSessionRunnerLogHelpers.StartResumeFallbackDiscoveryIfNeeded(fixture, null, "root", DateTimeOffset.UtcNow, options, NullLogger.Instance, default));
        options.EnableUncorrelatedNewSessionFileDiscovery = true;
        Assert.Null(CodexSessionRunnerLogHelpers.StartResumeFallbackDiscoveryIfNeeded(fixture, ExecEventPipelineBehaviorTests.Info, "root", DateTimeOffset.UtcNow, options, NullLogger.Instance, default));
        Assert.Equal("discovered", await CodexSessionRunnerLogHelpers.StartResumeFallbackDiscoveryIfNeeded(fixture, null, "root", DateTimeOffset.UtcNow, options, NullLogger.Instance, default)!);
        fixture.DiscoveryFailure = new IOException("discovery failed");
        var task = CodexSessionRunnerLogHelpers.StartResumeFallbackDiscoveryIfNeeded(fixture, null, "root", DateTimeOffset.UtcNow, options, NullLogger.Instance, default);
        Assert.Same(fixture.DiscoveryFailure, await Assert.ThrowsAsync<IOException>(() => task!));
    }

    [Fact]
    public void MaterializedSchema_IsUtf8WithoutBom_LeavesOptionsUnchanged_AndDeletesTempFiles()
    {
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt")
        {
            OutputSchema = CodexOutputSchema.FromJson(JsonSerializer.SerializeToElement(new { type = "object", title = "é" }))
        };
        var (effective, files) = CodexSessionRunnerLogHelpers.MaterializeOutputSchemaIfNeeded(options);
        try
        {
            var path = Assert.Single(files);
            Assert.NotSame(options, effective);
            Assert.Equal(CodexOutputSchemaKind.Json, options.OutputSchema.Kind);
            Assert.Equal(CodexOutputSchemaKind.File, effective.OutputSchema!.Kind);
            Assert.Equal(options.OutputSchema.Json!.Value.GetRawText(), File.ReadAllText(path));
            Assert.False(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
            CodexSessionRunnerLogHelpers.DeleteTempFilesBestEffort([path, path + ".absent"]);
            Assert.False(File.Exists(path));
        }
        finally { CodexSessionRunnerLogHelpers.DeleteTempFilesBestEffort(files); }
        var noSchema = new CodexSessionOptions(Path.GetTempPath(), "prompt");
        var unchanged = CodexSessionRunnerLogHelpers.MaterializeOutputSchemaIfNeeded(noSchema);
        Assert.Same(noSchema, unchanged.Effective);
        Assert.Empty(unchanged.TempFiles);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateHandleFromLog_SkipsUnrelatedEvents_OrRejectsMissingMetadata(bool includeMetadata)
    {
        var fixture = new Fixture { IncludeMetadata = includeMetadata };
        var create = () => CodexSessionRunnerLogHelpers.CreateHandleFromLogAsync("log", default, fixture, fixture,
            new MockCodexProcessLauncher(), TimeSpan.FromSeconds(1), NullLoggerFactory.Instance, NullLogger.Instance);
        if (!includeMetadata)
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(create);
            Assert.Contains("session_meta", failure.Message);
        }
        else
        {
            await using var handle = await create();
            Assert.Equal("metadata", handle.Info.Id.Value);
            Assert.Equal("log", handle.Info.LogPath);
            Assert.Equal("/work", handle.Info.WorkingDirectory);
            Assert.Equal("provider", handle.Info.ModelProvider);
            Assert.False(handle.IsLive);
        }
    }

    [Fact]
    public async Task MetadataStreamThatEndsOnCancellation_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture { OnEnumerationComplete = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CodexSessionRunnerLogHelpers.CreateHandleFromLogAsync(
            "log", cancellation.Token, fixture, fixture, new MockCodexProcessLauncher(), TimeSpan.FromSeconds(1), NullLoggerFactory.Instance, NullLogger.Instance));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfflineResume_UsesSelectedSessionOrFallsBackToSelectorLookup(bool selected)
    {
        var fixture = new Fixture { IncludeMetadata = true };
        if (selected) fixture.Sessions.Add(ExecEventPipelineBehaviorTests.Info);
        var runner = CreateRunner(fixture, new MockCodexProcessLauncher());
        await using var handle = await runner.ResumeSessionAsync(CodexResumeTarget.BySelector("test-session"), (string?)null, default);
        Assert.Equal(selected ? "log.jsonl" : "resolved-log", handle.Info.LogPath);
        Assert.Equal("metadata", handle.Info.Id.Value);
        Assert.False(handle.IsLive);
    }

    [Fact]
    public async Task OfflineMostRecentWithoutSessions_ReportsMissingTarget()
    {
        var runner = CreateRunner(new Fixture(), new MockCodexProcessLauncher());
        var failure = await Assert.ThrowsAsync<FileNotFoundException>(() => runner.ResumeSessionAsync(CodexResumeTarget.MostRecent(), (string?)null, default));
        Assert.Contains("--last", failure.Message);
    }

    [Fact]
    public async Task EphemeralStartAndResume_AreRejectedBeforeLaunchingProcess()
    {
        var launcher = new MockCodexProcessLauncher();
        var runner = CreateRunner(new Fixture(), launcher);
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt") { AdditionalOptions = ["--ephemeral"] };
        var start = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartSessionAsync(options, default));
        var resume = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ResumeSessionAsync(CodexResumeTarget.BySelector("session"), options, default));
        Assert.Contains("ephemeral", start.Message);
        Assert.Contains("ephemeral", resume.Message);
        Assert.Empty(launcher.CapturedStarts);
    }

    [Theory]
    [InlineData("tailer")]
    [InlineData("parser")]
    [InlineData("processLauncher")]
    [InlineData("loggerFactory")]
    [InlineData("logger")]
    public async Task CreateHandle_RejectsMissingDependencyBeforeReadingLog(string dependency)
    {
        var fixture = new Fixture { IncludeMetadata = true };
        var failure = await Assert.ThrowsAsync<ArgumentNullException>(() => CodexSessionRunnerLogHelpers.CreateHandleFromLogAsync("log", default,
            dependency == "tailer" ? null! : fixture, dependency == "parser" ? null! : fixture,
            dependency == "processLauncher" ? null! : new MockCodexProcessLauncher(), TimeSpan.FromSeconds(1),
            dependency == "loggerFactory" ? null! : NullLoggerFactory.Instance, dependency == "logger" ? null! : NullLogger.Instance));
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("sessionLocator")]
    [InlineData("target")]
    [InlineData("logger")]
    public async Task ResumeResolution_RejectsMissingDependencyEvenWhenFallbackIsAvailable(string dependency)
    {
        var failure = await Assert.ThrowsAsync<ArgumentNullException>(() => CodexSessionRunnerLogHelpers.ResolveResumeLogPathAsync(
            dependency == "sessionLocator" ? null! : new Fixture(), null, null, "root", Task.FromResult("fallback"), TimeSpan.FromSeconds(1),
            dependency == "target" ? null! : CodexResumeTarget.MostRecent(), dependency == "logger" ? null! : NullLogger.Instance, default));
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("sessionLocator")]
    [InlineData("clientOptions")]
    [InlineData("logger")]
    public void DisabledDiscovery_StillValidatesItsDependencies(string dependency)
    {
        var failure = Assert.Throws<ArgumentNullException>(() => { _ = CodexSessionRunnerLogHelpers.StartResumeFallbackDiscoveryIfNeeded(
            dependency == "sessionLocator" ? null! : new Fixture(), null, "root", DateTimeOffset.UtcNow,
            dependency == "clientOptions" ? null! : new CodexClientOptions(), dependency == "logger" ? null! : NullLogger.Instance, default); });
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("attach")]
    [InlineData("resume-id")]
    [InlineData("resume-target")]
    [InlineData("resume-live")]
    public async Task CanceledOperations_DoNotResolvePathsOrReadSessions(string operation)
    {
        var fixture = new Fixture { IncludeMetadata = true };
        var launcher = new MockCodexProcessLauncher();
        var runner = CreateRunner(fixture, launcher);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
        {
            "attach" => runner.AttachToLogAsync("log", cancellation.Token),
            "resume-id" => runner.ResumeSessionAsync(SessionId.Parse("session"), cancellation.Token),
            "resume-target" => runner.ResumeSessionAsync(CodexResumeTarget.MostRecent(), (string?)null, cancellation.Token),
            _ => runner.ResumeSessionAsync(CodexResumeTarget.BySelector("session"), new CodexSessionOptions(Path.GetTempPath(), "prompt"), cancellation.Token)
        });
        Assert.Equal(0, fixture.PathCalls);
        Assert.Equal(0, fixture.ValidateCalls);
        Assert.Empty(launcher.CapturedStarts);
    }

    [Fact]
    public async Task ResumeOverloads_RejectMissingTargetAndOptions()
    {
        var runner = CreateRunner(new Fixture(), new MockCodexProcessLauncher());
        Assert.Equal("target", (await Assert.ThrowsAsync<ArgumentNullException>(() => runner.ResumeSessionAsync(null!, (string?)null, default))).ParamName);
        Assert.Equal("target", (await Assert.ThrowsAsync<ArgumentNullException>(() => runner.ResumeSessionAsync((CodexResumeTarget)null!, new CodexSessionOptions(Path.GetTempPath(), "prompt"), default))).ParamName);
        Assert.Equal("options", (await Assert.ThrowsAsync<ArgumentNullException>(() => runner.ResumeSessionAsync(CodexResumeTarget.MostRecent(), (CodexSessionOptions)null!, default))).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => CodexSessionRunnerLogHelpers.MaterializeOutputSchemaIfNeeded(null!)).ParamName);
    }

    [Theory]
    [InlineData("clientOptions")]
    [InlineData("processLauncher")]
    [InlineData("sessionLocator")]
    [InlineData("tailer")]
    [InlineData("parser")]
    [InlineData("pathProvider")]
    [InlineData("loggerFactory")]
    [InlineData("logger")]
    public void Runner_RejectsMissingDependencies(string dependency)
    {
        var fixture = new Fixture();
        var failure = Assert.Throws<ArgumentNullException>(() => new CodexSessionRunner(
            dependency == "clientOptions" ? null! : new(), dependency == "processLauncher" ? null! : new MockCodexProcessLauncher(),
            dependency == "sessionLocator" ? null! : fixture, dependency == "tailer" ? null! : fixture,
            dependency == "parser" ? null! : fixture, dependency == "pathProvider" ? null! : fixture,
            dependency == "loggerFactory" ? null! : NullLoggerFactory.Instance, dependency == "logger" ? null! : NullLogger<CodexClient>.Instance));
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("clientOptions")]
    [InlineData("sessionLocator")]
    [InlineData("tailer")]
    [InlineData("parser")]
    [InlineData("pathProvider")]
    [InlineData("logger")]
    public void RateLimitsReader_RejectsMissingDependencies(string dependency)
    {
        var fixture = new Fixture();
        var failure = Assert.Throws<ArgumentNullException>(() => new CodexRateLimitsReader(
            dependency == "clientOptions" ? null! : new(), dependency == "sessionLocator" ? null! : fixture,
            dependency == "tailer" ? null! : fixture, dependency == "parser" ? null! : fixture,
            dependency == "pathProvider" ? null! : fixture, dependency == "logger" ? null! : NullLogger<CodexClient>.Instance));
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("clientOptions")]
    [InlineData("processLauncher")]
    [InlineData("sessionLocator")]
    [InlineData("pathProvider")]
    [InlineData("logger")]
    public void ReviewRunner_RejectsMissingDependencies(string dependency)
    {
        var fixture = new Fixture();
        var failure = Assert.Throws<ArgumentNullException>(() => new CodexReviewRunner(
            dependency == "clientOptions" ? null! : new(), dependency == "processLauncher" ? null! : new MockCodexProcessLauncher(),
            dependency == "sessionLocator" ? null! : fixture, dependency == "pathProvider" ? null! : fixture,
            dependency == "logger" ? null! : NullLogger<CodexClient>.Instance));
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("logger")]
    [InlineData("notifyExit")]
    [InlineData("tryStartIdleTermination")]
    public void EventPipeline_RejectsMissingDependencies(string dependency)
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new CodexSessionHandleEventPipeline(
            dependency == "info" ? null! : ExecEventPipelineBehaviorTests.Info, null, null, TimeSpan.FromSeconds(1),
            dependency == "logger" ? null! : NullLogger.Instance, dependency == "notifyExit" ? null! : (_, _) => { },
            dependency == "tryStartIdleTermination" ? null! : () => true));
        Assert.Equal(dependency, failure.ParamName);
    }

    private static CodexSessionRunner CreateRunner(Fixture fixture, MockCodexProcessLauncher launcher) =>
        new(new CodexClientOptions { CodexHomeDirectory = Path.Combine(Path.GetTempPath(), "codex-runner-test-" + Guid.NewGuid().ToString("N")) },
            launcher, fixture, fixture, fixture, fixture, NullLoggerFactory.Instance, NullLogger<CodexClient>.Instance);

    private sealed class Fixture : ICodexSessionLocator, IJsonlTailer, IJsonlEventParser, ICodexPathProvider
    {
        public List<CodexSessionInfo> Sessions { get; } = [];
        public Exception? LookupFailure;
        public Exception? DiscoveryFailure;
        public int LookupCalls;
        public int ValidateCalls;
        public int PathCalls;
        public bool IncludeMetadata;
        public Action? OnEnumerationComplete;
        public Task<string> WaitForSessionLogByIdAsync(SessionId id, string root, TimeSpan timeout, CancellationToken ct)
        {
            LookupCalls++;
            return LookupFailure is null ? Task.FromResult("captured") : Task.FromException<string>(LookupFailure);
        }
        public Task<string> WaitForNewSessionFileAsync(string root, DateTimeOffset start, TimeSpan timeout, CancellationToken ct) =>
            DiscoveryFailure is null ? Task.FromResult("discovered") : throw DiscoveryFailure;
        public Task<string> ValidateLogFileAsync(string path, CancellationToken ct) { ValidateCalls++; return Task.FromResult("validated:" + path); }
        public Task<string> FindSessionLogAsync(SessionId id, string root, CancellationToken ct) => Task.FromResult("resolved-log");
        public string GetSessionsRootDirectory(string? directory) { PathCalls++; return Path.GetTempPath(); }
        public string GetCodexExecutablePath(string? path) => throw new NotSupportedException();
        public string ResolveSessionLogPath(SessionId id, string? root) => throw new NotSupportedException();
        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string root, SessionFilter? filter, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            foreach (var session in Sessions) yield return session;
        }
        public async IAsyncEnumerable<string> TailAsync(string path, EventStreamOptions options, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield return "event";
        }
        public async IAsyncEnumerable<CodexEvent> ParseAsync(IAsyncEnumerable<string> lines, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var _ in lines.WithCancellation(ct)) yield return ExecEventPipelineBehaviorTests.Event();
            OnEnumerationComplete?.Invoke();
            if (IncludeMetadata) yield return new SessionMetaEvent
            {
                Timestamp = DateTimeOffset.UnixEpoch, Type = "session_meta", SessionId = SessionId.Parse("metadata"),
                Cwd = "/work", ModelProvider = "provider", RawPayload = JsonSerializer.SerializeToElement(new { })
            };
        }
    }
}
