using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.AppServer.Resiliency;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return Invalid(c => c.ResumeThreadAsync(" "));
        yield return Invalid(c => c.ResumeThreadAsync(new ThreadResumeOptions()));
        yield return Invalid(c => c.ReadThreadAsync(" "));
        yield return Invalid(c => c.UnsubscribeThreadAsync(" "));
        yield return Invalid(c => c.CompactThreadAsync(" "));
        yield return Invalid(c => c.RollbackThreadAsync(" ", 1));
        yield return Invalid(c => c.RollbackThreadAsync("thread-1", 0));
        yield return Invalid(c => c.CleanThreadBackgroundTerminalsAsync(" "));
        yield return Invalid(c => c.ArchiveThreadAsync(" "));
        yield return Invalid(c => c.DeleteThreadAsync(" "));
        yield return Invalid(c => c.UnarchiveThreadAsync(" "));
        yield return Invalid(c => c.SetThreadNameAsync(" ", "name"));
        yield return Invalid(c => c.SetThreadNameAsync("thread-1", " "));
        yield return Invalid(c => c.AppendThreadRealtimeTextAsync(" ", "text"));
        yield return Invalid(c => c.AppendThreadRealtimeTextAsync("thread-1", null!));
        yield return Invalid(c => c.AppendThreadRealtimeAudioAsync(" ", null!));
        yield return Invalid(c => c.AppendThreadRealtimeAudioAsync("thread-1", null!));
        yield return Invalid(c => c.StopThreadRealtimeAsync(" "));
        yield return Invalid(c => c.StartFuzzyFileSearchSessionAsync(" ", [PathForPlatform("/workspace")]));
        yield return Invalid(c => c.StartFuzzyFileSearchSessionAsync("session", []));
        yield return Invalid(c => c.StartFuzzyFileSearchSessionAsync("session", [" "]));
        yield return Invalid(c => c.UpdateFuzzyFileSearchSessionAsync(" ", "query"));
        yield return Invalid(c => c.UpdateFuzzyFileSearchSessionAsync("session", null!));
        yield return Invalid(c => c.StopFuzzyFileSearchSessionAsync(" "));
        yield return Invalid(c => c.ConsumeAccountRateLimitResetCreditAsync(" "));
        yield return Invalid(c => c.WriteRemoteSkillAsync(" ", false));
        yield return Invalid(c => c.CancelAccountLoginAsync(" "));
        yield return Invalid(c => c.ImportExternalAgentConfigAsync([null!]));
        yield return Invalid(c => c.StartWindowsSandboxSetupAsync(new WindowsSandboxSetupStartOptions(default)));
        yield return Invalid(c => c.CommandExecAsync(new() { Command = [] }));
        yield return Invalid(c => c.CommandExecWriteAsync(new() { ProcessId = " ", CloseStdin = true }));
        yield return Invalid(c => c.CommandExecResizeAsync(new() { ProcessId = " ", Size = new() { Columns = 1, Rows = 1 } }));
        yield return Invalid(c => c.CommandExecResizeAsync(new() { ProcessId = "proc-1", Size = new() { Columns = 1, Rows = 0 } }));
        yield return Invalid(c => c.CommandExecTerminateAsync(new() { ProcessId = " " }));
        yield return Invalid(c => c.FsWriteFileAsync(new() { Path = PathForPlatform("/workspace/file"), DataBase64 = null! }));
        yield return Invalid(c => c.FsReadFileAsync(new() { Path = " " }));
        yield return Invalid(c => c.FsWatchAsync(new() { Path = PathForPlatform("/workspace"), WatchId = " " }));
        yield return Invalid(c => c.FsUnwatchAsync(new() { WatchId = " " }));
        yield return Invalid(c => c.ReadAppsAsync(new() { AppIds = [] }));
        yield return Invalid(c => c.AddEnvironmentAsync(new() { EnvironmentId = " ", ExecServerUrl = "https://example.test" }));
        yield return Invalid(c => c.AddEnvironmentAsync(new() { EnvironmentId = "env", ExecServerUrl = " " }));
        yield return Invalid(c => c.SearchPluginsAsync(new() { SearchTerm = " " }));
        yield return Invalid(c => c.ReadPluginAsync(new() { RemoteMarketplaceName = "catalog", PluginName = " " }));
        yield return Invalid(c => c.InstallPluginAsync(new() { RemoteMarketplaceName = "catalog", PluginName = " " }));
        yield return Invalid(c => c.UninstallPluginAsync(new() { PluginId = " " }));
        yield return Invalid(c => c.StartMcpServerOauthLoginAsync(new() { Name = " " }));
        yield return Invalid(c => c.ReadMcpResourceAsync(new() { Server = "server", Uri = " " }));
        yield return Invalid(c => c.WriteSkillsConfigAsync(new() { Enabled = true }));
        yield return Invalid(c => c.WriteSkillsConfigAsync(new() { Enabled = true, Name = "named", Path = PathForPlatform("/skill") }));
        yield return Invalid(c => c.StartTurnAsync(" ", new()));
        yield return Invalid(c => c.SteerTurnAsync(new() { ThreadId = " ", ExpectedTurnId = "turn-1" }));
        yield return Invalid(c => c.SteerTurnAsync(new() { ThreadId = "thread-1", ExpectedTurnId = " " }));
        yield return Invalid(c => c.StartReviewAsync(new() { ThreadId = " ", Target = new ReviewTarget.UncommittedChanges() }));
        yield return Invalid(c => c.StartReviewAsync(new() { ThreadId = "thread-1", Target = null! }));
        yield return Invalid(c => c.AddThreadAttachmentAsync(new() { ThreadId = "thread-1", AttachmentType = "review", IdentityKey = "review-1", Payload = default }));
        yield return Invalid(c => c.ListThreadAttachmentsAsync(new() { ThreadId = " " }));
        yield return Invalid(c => c.RemoveThreadAttachmentAsync(new() { ThreadId = " ", AttachmentType = "review", IdentityKey = "review-1" }));
        yield return Invalid(c => c.RemoveThreadAttachmentAsync(new() { ThreadId = "thread-1", AttachmentType = " ", IdentityKey = "review-1" }));
        yield return Invalid(c => c.RemoveThreadAttachmentAsync(new() { ThreadId = "thread-1", AttachmentType = "review", IdentityKey = " " }));
        yield return Invalid(c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = " " }));
        yield return Invalid(c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = "thread-1", SessionId = " " }));
        yield return Invalid(c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = "thread-1", Voice = " " }));
        yield return Invalid(c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = "thread-1", PromptMode = ThreadRealtimePromptMode.Custom }));
        yield return Invalid(c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = "thread-1", PromptMode = ThreadRealtimePromptMode.Default, Prompt = "not allowed" }));
        yield return Invalid(c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = "thread-1", PromptMode = ThreadRealtimePromptMode.None, Prompt = "not allowed" }));
        yield return Invalid(c => c.SearchThreadsAsync(new() { SearchTerm = " " }));
        yield return Invalid(c => c.GetThreadGoalAsync(" "));
        yield return Invalid(c => c.ClearThreadGoalAsync(" "));
        yield return Invalid(c => c.SetThreadGoalAsync(new() { ThreadId = " " }));
        yield return Invalid(c => c.SetThreadGoalAsync(new() { ThreadId = "thread-1", Objective = " " }));
        yield return Invalid(c => c.ThreadShellCommandAsync(new() { ThreadId = " ", Command = "echo hello" }));
        yield return Invalid(c => c.ThreadShellCommandAsync(new() { ThreadId = "thread-1", Command = " " }));
        yield return Invalid(c => c.UpdateThreadMetadataAsync(new() { ThreadId = " " }));
        yield return Invalid(c => c.UpdateThreadMetadataAsync(new() { ThreadId = "thread-1", UpdateProjectId = true, ClearProjectId = true }));
        yield return Invalid(c => c.UpdateThreadMetadataAsync(new() { ThreadId = "thread-1", UpdateProjectId = true, ProjectId = " " }));
        yield return Invalid(c => c.UpdateThreadSettingsAsync(new() { ThreadId = " " }));
        yield return Invalid(c => c.ListThreadsAsync(new() { UnsectionedOnly = true, SectionId = "section-1" }));
        yield return Invalid(c => c.UpdateThreadSectionAsync(new() { SectionId = "section-1", Name = "Planning", ClearAppearance = true, Appearance = new() }));
        yield return Invalid(c => c.CommandExecAsync(new() { Command = ["echo"], DisableOutputCap = true, OutputBytesCap = 1 }));
        yield return Invalid(c => c.CommandExecAsync(new() { Command = ["echo"], Size = new() { Columns = 80, Rows = 24 }, Tty = false }));
        yield return Invalid(c => c.StartThreadAsync(new() { ClearServiceTier = true, ServiceTier = JKToolKit.CodexSDK.Models.CodexServiceTier.Fast }));
        yield return Invalid(c => c.StartThreadAsync(new() { Environments = [new() { EnvironmentId = " ", Cwd = PathForPlatform("/workspace") }] }));
        yield return Invalid(c => c.StartTurnAsync("thread-1", new() { AdditionalContext = new Dictionary<string, TurnAdditionalContextEntry> { ["context"] = null! } }));
        yield return Invalid(c => c.ResumeThreadAsync(new ThreadResumeOptions { ThreadId = "thread-1", InitialTurnsPage = new() { Limit = -1 } }));

    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidArguments_AreRejectedBeforeRpcDispatch(Func<ResilientCodexAppServerClient, Task> operation)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => operation(client);
        await action.Should().ThrowAsync<ArgumentException>();
        rpc.Requests.Should().BeEmpty();
    }

    public static IEnumerable<object[]> ExperimentalRequests()
    {
        yield return Invalid(c => c.StartFuzzyFileSearchSessionAsync("session", [PathForPlatform("/workspace")]));
        yield return Invalid(c => c.UpdateFuzzyFileSearchSessionAsync("session", "query"));
        yield return Invalid(c => c.StopFuzzyFileSearchSessionAsync("session"));
        yield return Invalid(c => c.AppendThreadRealtimeTextAsync("thread-1", "text"));
        yield return Invalid(c => c.StopThreadRealtimeAsync("thread-1"));
        yield return Invalid(c => c.CleanThreadBackgroundTerminalsAsync("thread-1"));
        yield return Invalid(c => c.ListCollaborationModesAsync());
        yield return Invalid(c => c.AddEnvironmentAsync(new() { EnvironmentId = "env", ExecServerUrl = "https://example.test" }));
    }

    [Theory]
    [MemberData(nameof(ExperimentalRequests))]
    public async Task ExperimentalOperations_RequireNegotiatedCapability(Func<ResilientCodexAppServerClient, Task> operation)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc, experimentalApi: false));
        var action = () => operation(client);
        await action.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[{}]")]
    public async Task ResumeHistory_UsesPlaceholderOnlyForNonemptyHistory(string history)
    {
        var rpc = new RecordingRpc("""{"thread":{"id":"restored"}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var options = new ThreadResumeOptions { History = JsonDocument.Parse(history).RootElement.Clone() };
        if (history is "null" or "[]")
        {
            var action = () => client.ResumeThreadAsync(options);
            await action.Should().ThrowAsync<ArgumentException>();
            rpc.Requests.Should().BeEmpty();
        }
        else
        {
            (await client.ResumeThreadAsync(options)).Id.Should().Be("restored");
            rpc.Requests[0].Parameters.GetProperty("threadId").GetString().Should().BeEmpty();
            JsonElement.DeepEquals(rpc.Requests[0].Parameters.GetProperty("history"), options.History.Value).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("start")]
    [InlineData("fork")]
    [InlineData("resume")]
    public async Task LifecycleResponses_RequireAnIdentifierWhenNoFallbackExists(string operation)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        Func<Task> action = operation switch
        {
            "start" => () => client.StartThreadAsync(new()),
            "fork" => () => client.ForkThreadAsync(new() { ThreadId = "original" }),
            _ => () => client.ResumeThreadAsync(new ThreadResumeOptions { Path = PathForPlatform("/rollout.jsonl") })
        };
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*returned no thread id*");
        rpc.Requests.Should().ContainSingle();
    }

    private static object[] Invalid(Func<ResilientCodexAppServerClient, Task> operation) => [operation];
}
