using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    public static IEnumerable<object[]> Requests()
    {
        yield return Case("thread/loaded/list", c => c.ListLoadedThreadsAsync(new() { Cursor = "page", Limit = 7 }), """{"cursor":"page","limit":7}""", """{"data":["thread-1"],"nextCursor":"next"}""");
        yield return Case("thread/unsubscribe", c => c.UnsubscribeThreadAsync("thread-1"), """{"threadId":"thread-1"}""", """{"status":"notSubscribed"}""");
        yield return Case("thread/compact/start", c => c.CompactThreadAsync("thread-1"), """{"threadId":"thread-1"}""");
        yield return Case("thread/backgroundTerminals/clean", c => c.CleanThreadBackgroundTerminalsAsync("thread-1"), """{"threadId":"thread-1"}""");
        yield return Case("thread/rollback", c => c.RollbackThreadAsync("thread-1", 3), """{"threadId":"thread-1","numTurns":3}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("thread/archive", c => c.ArchiveThreadAsync("thread-1"), """{"threadId":"thread-1"}""");
        yield return Case("thread/delete", c => c.DeleteThreadAsync("thread-1"), """{"threadId":"thread-1"}""");
        yield return Case("thread/unarchive", c => c.UnarchiveThreadAsync("thread-1"), """{"threadId":"thread-1"}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("thread/name/set", c => c.SetThreadNameAsync("thread-1", "Planning"), """{"threadId":"thread-1","name":"Planning"}""");
        yield return Case("thread/read", c => c.ReadThreadAsync("thread-1"), """{"threadId":"thread-1"}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("thread/read", c => c.ReadThreadAsync("thread-1", new ThreadReadOptions { IncludeTurns = true }), """{"threadId":"thread-1","includeTurns":true}""", """{"thread":{"id":"thread-1","turns":[]}}""");
        yield return Case("thread/resume", c => c.ResumeThreadAsync("thread-1"), """{"threadId":"thread-1"}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("thread/resume", c => c.ResumeThreadAsync(new ThreadResumeOptions { ThreadId = "thread-1" }), """{"threadId":"thread-1"}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("thread/start", c => c.StartThreadAsync(new() { Cwd = PathForPlatform("/workspace") }), """{"cwd":"/workspace"}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("thread/fork", c => c.ForkThreadAsync(new() { ThreadId = "thread-1" }), """{"threadId":"thread-1"}""", """{"thread":{"id":"thread-2"}}""");
        yield return Case("thread/list", c => c.ListThreadsAsync(new() { Cursor = "page", Limit = 7 }), """{"cursor":"page","limit":7}""", """{"data":[]}""");
        yield return Case("thread/goal/get", c => c.GetThreadGoalAsync("thread-1"), """{"threadId":"thread-1"}""", """{"goal":null}""");
        yield return Case("thread/goal/clear", c => c.ClearThreadGoalAsync("thread-1"), """{"threadId":"thread-1"}""", """{"cleared":true}""");
        yield return Case("thread/goal/set", c => c.SetThreadGoalAsync(new() { ThreadId = "thread-1", Objective = "Ship" }), """{"threadId":"thread-1","objective":"Ship"}""", """{"goal":null}""");
        yield return Case("thread/realtime/start", c => c.StartThreadRealtimeAsync("thread-1", "Assist", "session-1"), """{"threadId":"thread-1","sessionId":"session-1"}""");
        yield return Case("thread/realtime/start", c => c.StartThreadRealtimeAsync(new ThreadRealtimeStartOptions { ThreadId = "thread-1", Prompt = "Assist", PromptMode = ThreadRealtimePromptMode.Custom }), """{"threadId":"thread-1"}""");
        yield return Case("thread/realtime/appendText", c => c.AppendThreadRealtimeTextAsync("thread-1", "hello"), """{"threadId":"thread-1","text":"hello"}""");
        yield return Case("thread/realtime/stop", c => c.StopThreadRealtimeAsync("thread-1"), """{"threadId":"thread-1"}""");
        yield return Case("fuzzyFileSearch/sessionStart", c => c.StartFuzzyFileSearchSessionAsync("search-1", [PathForPlatform("/workspace")]), """{"sessionId":"search-1","roots":["/workspace"]}""");
        yield return Case("fuzzyFileSearch/sessionUpdate", c => c.UpdateFuzzyFileSearchSessionAsync("search-1", "readme"), """{"sessionId":"search-1","query":"readme"}""");
        yield return Case("fuzzyFileSearch/sessionStop", c => c.StopFuzzyFileSearchSessionAsync("search-1"), """{"sessionId":"search-1"}""");
        yield return Case("getAuthStatus", c => c.GetAuthStatusAsync(), "{}", """{"authMethod":"apikey","authToken":"token","requiresOpenaiAuth":true}""");
        yield return Case("getAuthStatus", c => c.GetAuthStatusAsync(new AuthStatusOptions { IncludeToken = true, RefreshToken = false }), """{"includeToken":true,"refreshToken":false}""");
        yield return Case("gitDiffToRemote", c => c.GetGitDiffToRemoteAsync(new() { Cwd = PathForPlatform("/workspace") }), """{"cwd":"/workspace"}""", """{"sha":"abc","diff":"patch"}""");
        yield return Case("account/read", c => c.ReadAccountAsync(), "{}", """{"account":null,"requiresOpenaiAuth":true}""");
        yield return Case("account/read", c => c.ReadAccountAsync(new AccountReadOptions { RefreshToken = true }), """{"refreshToken":true}""", """{"account":null,"requiresOpenaiAuth":true}""");
        yield return Case("account/rateLimits/read", c => c.ReadAccountRateLimitsAsync(), "null");
        yield return Case("account/rateLimits/read", c => c.ReadAccountRateLimitsAsync(new AccountRateLimitsReadOptions { SupportsLunaReserve = true, ExcludeResetCreditDetails = false }), """{"supportsLunaReserve":true,"excludeResetCreditDetails":false}""");
        yield return Case("account/logout", c => c.LogoutAccountAsync(), "null");
        yield return Case("model/list", c => c.ListModelsAsync(), "{}", """{"data":[]}""");
        yield return Case("model/list", c => c.ListModelsAsync(new ModelListOptions { Limit = 4, Cursor = "m" }), """{"limit":4,"cursor":"m"}""", """{"data":[]}""");
        yield return Case("experimentalFeature/list", c => c.ListExperimentalFeaturesAsync(), "{}", """{"data":[]}""");
        yield return Case("experimentalFeature/list", c => c.ListExperimentalFeaturesAsync(new ExperimentalFeatureListOptions { Limit = 4, Cursor = "f" }), """{"limit":4,"cursor":"f"}""", """{"data":[]}""");
        yield return Case("windowsSandbox/setupStart", c => c.StartWindowsSandboxSetupAsync("elevated"), """{"mode":"elevated"}""", """{"started":true}""");
        yield return Case("windowsSandbox/setupStart", c => c.StartWindowsSandboxSetupAsync(new WindowsSandboxSetupStartOptions(WindowsSandboxSetupMode.Elevated) { Cwd = PathForPlatform("/workspace") }), """{"mode":"elevated","cwd":"/workspace"}""", """{"started":false}""");
        yield return Case("windowsSandbox/setupStart", c => c.StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode.Elevated, PathForPlatform("/workspace")), """{"mode":"elevated","cwd":"/workspace"}""", """{"started":true}""");
        yield return Case("config/mcpServer/reload", c => c.ReloadMcpServersAsync(), "null");
        yield return Case("mcpServerStatus/list", c => c.ListMcpServerStatusAsync(new() { Limit = 2, Cursor = "mcp" }), """{"limit":2,"cursor":"mcp"}""", """{"data":[]}""");
        yield return Case("skills/remote/list", c => c.ReadRemoteSkillsAsync(), "{}", """{"data":[]}""");
        yield return Case("skills/remote/export", c => c.WriteRemoteSkillAsync("skill-1", true), """{"hazelnutId":"skill-1","isPreload":true}""");
        yield return Case("skills/config/write", c => c.WriteSkillsConfigAsync(true, PathForPlatform("/workspace/SKILL.md")), """{"enabled":true,"path":"/workspace/SKILL.md"}""");
        yield return Case("skills/config/write", c => c.WriteSkillsConfigAsync(new() { Enabled = false, Name = "example" }), """{"enabled":false,"name":"example"}""");
        yield return Case("configRequirements/read", c => c.ReadConfigRequirementsAsync(), "null");
        yield return Case("externalAgentConfig/detect", c => c.DetectExternalAgentConfigAsync(new()), "{}", """{"items":[]}""");
        yield return Case("externalAgentConfig/import", c => c.ImportExternalAgentConfigAsync([]), """{"migrationItems":[]}""");
        yield return Case("skills/list", c => c.ListSkillsAsync(new() { Cwd = PathForPlatform("/workspace"), ForceReload = true }), """{"cwds":["/workspace"],"forceReload":true}""", """{"data":[]}""");
        yield return Case("skills/extraRoots/set", c => c.SetSkillsExtraRootsAsync(new() { ExtraRoots = [PathForPlatform("/skills")] }), """{"extraRoots":["/skills"]}""");
        yield return Case("app/list", c => c.ListAppsAsync(new() { ThreadId = "thread-1", ForceRefetch = true, Limit = 5 }), """{"threadId":"thread-1","forceRefetch":true,"limit":5}""", """{"data":[]}""");
        yield return Case("app/read", c => c.ReadAppsAsync(new() { AppIds = ["app-1"], IncludeTools = true }), """{"appIds":["app-1"],"includeTools":true}""", """{"apps":[],"notFoundAppIds":[]}""");
        yield return Case("app/installed", c => c.ReadInstalledAppsAsync(new()), "{}", """{"apps":[]}""");
        yield return Case("collaborationMode/list", c => c.ListCollaborationModesAsync(), "{}", """{"data":[]}""");
        yield return Case("environment/add", c => c.AddEnvironmentAsync(new() { EnvironmentId = "env-1", ExecServerUrl = "https://example.test", AuthBearerToken = "test-token" }), """{"environmentId":"env-1","execServerUrl":"https://example.test","authBearerToken":"test-token"}""");
        yield return Case("account/workspaceMessages/read", c => c.ReadWorkspaceMessagesAsync(), "null", """{"featureEnabled":true,"messages":[]}""");
        yield return Case("account/usage/read", c => c.ReadAccountTokenUsageAsync(), "null", """{"summary":{}}""");
        yield return Case("account/usage/read", c => c.ReadAccountTokenUsageAsync(new AccountTokenUsageReadOptions { ThreadId = "thread-1" }), """{"threadId":"thread-1"}""", """{"summary":{}}""");
        yield return Case("account/rateLimitResetCredit/consume", c => c.ConsumeAccountRateLimitResetCreditAsync("once-1"), """{"idempotencyKey":"once-1"}""", """{"outcome":"reset"}""");
        yield return Case("mcpServer/oauth/login", c => c.StartMcpServerOauthLoginAsync(new() { Name = "server", Scopes = ["read"], TimeoutSeconds = 30 }), """{"name":"server","scopes":["read"],"timeoutSecs":30}""", """{"authorizationUrl":"https://example.test/auth"}""");
        yield return Case("mcpResource/read", c => c.ReadMcpResourceAsync(new() { Server = "server", Uri = "resource://one" }), """{"server":"server","uri":"resource://one"}""", """{"contents":[]}""");
        yield return Case("command/exec", c => c.CommandExecAsync(new() { Command = ["echo", "hello"], Cwd = PathForPlatform("/workspace") }), """{"command":["echo","hello"],"cwd":"/workspace"}""", """{"exitCode":0,"stdout":"hello","stderr":""}""");
        yield return Case("command/exec/write", c => c.CommandExecWriteAsync(new() { ProcessId = "proc-1", CloseStdin = true }), """{"processId":"proc-1","closeStdin":true}""");
        yield return Case("command/exec/resize", c => c.CommandExecResizeAsync(new() { ProcessId = "proc-1", Size = new() { Columns = 80, Rows = 24 } }), """{"processId":"proc-1","size":{"cols":80,"rows":24}}""");
        yield return Case("command/exec/terminate", c => c.CommandExecTerminateAsync(new() { ProcessId = "proc-1" }), """{"processId":"proc-1"}""");
        yield return Case("fs/readFile", c => c.FsReadFileAsync(new() { Path = PathForPlatform("/workspace/file") }), """{"path":"/workspace/file"}""", """{"dataBase64":"YQ=="}""");
        yield return Case("fs/writeFile", c => c.FsWriteFileAsync(new() { Path = PathForPlatform("/workspace/file"), DataBase64 = "YQ==" }), """{"path":"/workspace/file","dataBase64":"YQ=="}""");
        yield return Case("fs/createDirectory", c => c.FsCreateDirectoryAsync(new() { Path = PathForPlatform("/workspace/folder"), Recursive = true }), """{"path":"/workspace/folder","recursive":true}""");
        yield return Case("fs/getMetadata", c => c.FsGetMetadataAsync(new() { Path = PathForPlatform("/workspace/file") }), """{"path":"/workspace/file"}""", """{"isDirectory":false,"isFile":true,"createdAtMs":1,"modifiedAtMs":2}""");
        yield return Case("fs/readDirectory", c => c.FsReadDirectoryAsync(new() { Path = PathForPlatform("/workspace") }), """{"path":"/workspace"}""", """{"entries":[]}""");
        yield return Case("fs/remove", c => c.FsRemoveAsync(new() { Path = PathForPlatform("/workspace/file"), Force = true }), """{"path":"/workspace/file","force":true}""");
        yield return Case("fs/copy", c => c.FsCopyAsync(new() { SourcePath = PathForPlatform("/workspace/a"), DestinationPath = PathForPlatform("/workspace/b") }), """{"sourcePath":"/workspace/a","destinationPath":"/workspace/b"}""");
        yield return Case("fs/watch", c => c.FsWatchAsync(new() { Path = PathForPlatform("/workspace"), WatchId = "watch-1" }), """{"path":"/workspace","watchId":"watch-1"}""", """{"path":"/workspace"}""");
        yield return Case("fs/unwatch", c => c.FsUnwatchAsync(new() { WatchId = "watch-1" }), """{"watchId":"watch-1"}""");
        yield return Case("plugin/list", c => c.ListPluginsAsync(new() { Cwds = [PathForPlatform("/workspace")], ForceRefetch = true }), """{"cwds":["/workspace"],"forceRefetch":true}""", """{"marketplaces":[]}""");
        yield return Case("plugin/search", c => c.SearchPluginsAsync(new() { SearchTerm = "calendar" }), """{"searchTerm":"calendar"}""", """{"data":[]}""");
        yield return Case("plugin/install", c => c.InstallPluginAsync(new() { RemoteMarketplaceName = "catalog", PluginName = "calendar" }), """{"remoteMarketplaceName":"catalog","pluginName":"calendar"}""", """{"appsNeedingAuth":[],"authPolicy":"ON_INSTALL"}""");
        yield return Case("plugin/uninstall", c => c.UninstallPluginAsync(new() { PluginId = "calendar" }), """{"pluginId":"calendar"}""");
        yield return Case("plugin/reconcile", c => c.ReconcilePluginsAsync(new()), "{}", """{"changedPlugins":[],"failedRemotePluginIds":[],"failedMaterializationRemotePluginIds":[]}""");
        yield return Case("threadSection/list", c => c.ListThreadSectionsAsync(new() { Cursor = "page", Limit = 2 }), """{"cursor":"page","limit":2}""", """{"data":[]}""");
        yield return Case("threadSection/delete", c => c.DeleteThreadSectionAsync(new() { SectionId = "section-1" }), """{"sectionId":"section-1"}""");
        yield return Case("thread/section/move", c => c.MoveThreadToSectionAsync(new() { ThreadId = "thread-1", SectionId = "section-1", BeforeThreadId = "thread-2" }), """{"threadId":"thread-1","sectionId":"section-1","beforeThreadId":"thread-2"}""");

        yield return Case("config/read", c => c.ReadConfigAsync(new() { IncludeLayers = true, Cwd = PathForPlatform("/workspace") }), """{"includeLayers":true,"cwd":"/workspace"}""", """{"config":{}}""");
        yield return Case("config/value/write", c => c.WriteConfigValueAsync(new() { KeyPath = "model", Value = JsonSerializer.SerializeToElement("example"), MergeStrategy = ConfigMergeStrategy.Replace }), """{"keyPath":"model","value":"example","mergeStrategy":"replace"}""", """{"status":"ok","version":"v1","filePath":"/config.toml"}""");
        yield return Case("config/batchWrite", c => c.WriteConfigBatchAsync(new() { Edits = [new() { KeyPath = "model", Value = JsonSerializer.SerializeToElement("example"), MergeStrategy = ConfigMergeStrategy.Upsert }], ReloadUserConfig = true }), """{"edits":[{"keyPath":"model","value":"example","mergeStrategy":"upsert"}],"reloadUserConfig":true}""", """{"status":"okOverridden","version":"v2","filePath":"/config.toml"}""");
        yield return Case("feedback/upload", c => c.UploadFeedbackAsync(new() { Classification = "bug", ThreadId = "thread-1", IncludeLogs = true }), """{"classification":"bug","threadId":"thread-1","includeLogs":true}""", """{"threadId":"thread-1","promptHash":"hash"}""");
        yield return Case("remoteControl/enable", c => c.EnableRemoteControlAsync(), "{}", """{"status":"connected","serverName":"server","installationId":"install-1"}""");
        yield return Case("remoteControl/disable", c => c.DisableRemoteControlAsync(), "{}", """{"status":"disabled","serverName":"server","installationId":"install-1"}""");
        yield return Case("remoteControl/status/read", c => c.ReadRemoteControlStatusAsync(), "{}", """{"status":"connecting","serverName":"server","installationId":"install-1"}""");
        yield return Case("remoteControl/pairing/start", c => c.StartRemoteControlPairingAsync(new() { ManualCode = true }), """{"manualCode":true}""", """{"pairingCode":"pair","environmentId":"env-1","expiresAt":123}""");
        yield return Case("remoteControl/pairing/status", c => c.ReadRemoteControlPairingStatusAsync(new() { PairingCode = "pair" }), """{"pairingCode":"pair"}""", """{"claimed":true}""");
        yield return Case("remoteControl/client/list", c => c.ListRemoteControlClientsAsync(new() { EnvironmentId = "env-1", Limit = 2 }), """{"environmentId":"env-1","limit":2}""", """{"data":[]}""");
        yield return Case("remoteControl/client/revoke", c => c.RevokeRemoteControlClientAsync(new() { EnvironmentId = "env-1", ClientId = "client-1" }), """{"environmentId":"env-1","clientId":"client-1"}""");
        yield return Case("thread/attachment/list", c => c.ListThreadAttachmentsAsync(new() { ThreadId = "thread-1", Limit = 2 }), """{"threadId":"thread-1","limit":2}""", """{"data":[]}""");
        yield return Case("thread/attachment/remove", c => c.RemoveThreadAttachmentAsync(new() { ThreadId = "thread-1", AttachmentType = "review", IdentityKey = "review-1" }), """{"threadId":"thread-1","attachmentType":"review","identityKey":"review-1"}""");
        yield return Case("thread/settings/update", c => c.UpdateThreadSettingsAsync(new() { ThreadId = "thread-1", DisabledPluginIds = ["plugin-1"] }), """{"threadId":"thread-1","disabledPluginIds":["plugin-1"]}""");
        yield return Case("permissionProfile/list", c => c.ListPermissionProfilesAsync(new()), "{}", """{"data":[]}""");

        yield return Case("plugin/share/save", c => c.SavePluginShareAsync(new() { PluginPath = PathForPlatform("/plugins/example"), Discoverability = PluginShareDiscoverability.Unlisted }), """{"pluginPath":"/plugins/example","discoverability":"UNLISTED"}""", """{"remotePluginId":"remote-1","shareUrl":"https://example.test/share"}""");
        yield return Case("plugin/share/updateTargets", c => c.UpdatePluginShareTargetsAsync(new() { RemotePluginId = "remote-1", Discoverability = PluginShareUpdateDiscoverability.Private, ShareTargets = [] }), """{"remotePluginId":"remote-1","discoverability":"PRIVATE","shareTargets":[]}""", """{"principals":[],"discoverability":"PRIVATE"}""");
        yield return Case("plugin/share/list", c => c.ListPluginSharesAsync(), "{}", """{"data":[]}""");
        yield return Case("plugin/share/checkout", c => c.CheckoutPluginShareAsync(new() { RemotePluginId = "remote-1" }), """{"remotePluginId":"remote-1"}""", """{"remotePluginId":"remote-1","pluginId":"plugin-1","pluginName":"Example","pluginPath":"/plugins/example","marketplaceName":"catalog","marketplacePath":"/marketplace"}""");
        yield return Case("plugin/share/delete", c => c.DeletePluginShareAsync(new() { RemotePluginId = "remote-1" }), """{"remotePluginId":"remote-1"}""");
        yield return Case("threadSection/create", c => c.CreateThreadSectionAsync(new() { Name = "Planning" }), """{"name":"Planning"}""", """{"section":{"id":"section-1","name":"Planning"}}""");
        yield return Case("threadSection/update", c => c.UpdateThreadSectionAsync(new() { SectionId = "section-1", Name = "Planned", Appearance = new() { Color = "blue" } }), """{"sectionId":"section-1","name":"Planned","appearance":{"color":"blue","icon":null}}""", """{"section":{"id":"section-1","name":"Planned"}}""");
        yield return Case("thread/search", c => c.SearchThreadsAsync(new() { SearchTerm = "planning", Limit = 2 }), """{"searchTerm":"planning","limit":2}""", """{"data":[]}""");
        yield return Case("thread/attachment/add", c => c.AddThreadAttachmentAsync(new() { ThreadId = "thread-1", AttachmentType = "review", IdentityKey = "review-1", Payload = JsonSerializer.SerializeToElement(new { status = "open" }) }), """{"threadId":"thread-1","attachmentType":"review","identityKey":"review-1","payload":{"status":"open"}}""", """{"outcome":"created","attachment":{"id":"att-1","attachmentType":"review","identityKey":"review-1","payload":{"status":"open"},"createdAt":1}}""");
        yield return Case("thread/shellCommand", c => c.ThreadShellCommandAsync(new() { ThreadId = "thread-1", Command = "echo hello", TimeoutMs = 100 }), """{"threadId":"thread-1","command":"echo hello","timeoutMs":100}""");
        yield return Case("thread/metadata/update", c => c.UpdateThreadMetadataAsync(new() { ThreadId = "thread-1", ClearProjectId = true }), """{"threadId":"thread-1","projectId":""}""", """{"thread":{"id":"thread-1"}}""");
        yield return Case("experimentalFeature/enablement/set", c => c.SetExperimentalFeatureEnablementAsync(new() { Enablement = new Dictionary<string, bool> { ["test"] = true } }), """{"enablement":{"test":true}}""", """{"enablement":{"test":true}}""");

        yield return Case("account/login/start", c => c.StartAccountLoginAsync(new AccountLoginStartOptions.ApiKey("test-only-key")), """{"type":"apiKey","apiKey":"test-only-key"}""", """{"type":"apiKey"}""");

    }

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task ResilientClient_UsesRealAdapterAndTransportsOperation(string method, Func<ResilientCodexAppServerClient, Task> operation, string expectedParameters, string response)
    {
        var rpc = new RecordingRpc(response);
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        await operation(client).WaitAsync(TimeSpan.FromSeconds(5));
        rpc.Requests.Should().ContainSingle();
        rpc.Requests[0].Method.Should().Be(method);
        AssertSubset(JsonDocument.Parse(WireJsonForPlatform(expectedParameters)).RootElement, rpc.Requests[0].Parameters);
        client.State.Should().Be(CodexAppServerConnectionState.Connected);
        client.RestartCount.Should().Be(0);
    }

    [Fact]
    public async Task GenericCalls_DeserializeUsingCallerOptions_AndSurfaceRemoteErrors()
    {
        var rpc = new RecordingRpc("""{"VALUE":42}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var raw = await client.CallAsync("custom/read", new { id = "one" });
        raw.GetProperty("VALUE").GetInt32().Should().Be(42);
        var typed = await client.CallAsync<Reply>("custom/typed", new { id = "two" }, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        typed!.Value.Should().Be(42);
        rpc.Failure = new JsonRpcRemoteException(new JsonRpcError(-32001, "denied", null));
        var action = () => client.CallAsync("custom/fail", null);
        (await action.Should().ThrowAsync<JsonRpcRemoteException>()).Which.Should().BeSameAs(rpc.Failure);
        rpc.Requests.Select(x => x.Method).Should().Equal("custom/read", "custom/typed", "custom/fail");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DependencyInjection_StartsThroughRegisteredFactory(bool configure)
    {
        var services = new ServiceCollection();
        var rpc = new RecordingRpc("""{"VALUE":42}""");
        services.AddSingleton<ICodexAppServerClientFactory>(new Factory(rpc));
        services.AddCodexResilientAppServerClient(configure ? o => o.AutoRestart = false : null);
        using var provider = services.BuildServiceProvider();
        await using var client = await provider.GetRequiredService<ICodexResilientAppServerClientFactory>().StartAsync();
        (await client.CallAsync("custom/read", null)).GetProperty("VALUE").GetInt32().Should().Be(42);
        client.InitializeResult.Should().BeNull();
        client.NotificationDropStats.Should().NotBeNull();
        client.ExitTask.IsCompleted.Should().BeFalse();
        await client.DisposeAsync();
        await client.ExitTask.WaitAsync(TimeSpan.FromSeconds(5));
        rpc.Disposed.Should().BeTrue();
    }

    private static object[] Case(string method, Func<ResilientCodexAppServerClient, Task> call, string expected, string response = "{}") => [method, call, expected, response];

    private static object[] Case<T>(string method, Func<ResilientCodexAppServerClient, Task<T>> call, string expected, string response = "{}") =>
        Case(method, async client =>
        {
            var result = await call(client);
            result.Should().NotBeNull();
            var serialized = JsonSerializer.SerializeToElement(result, CodexAppServerClient.CreateDefaultSerializerOptions());
            if (serialized.ValueKind == JsonValueKind.Object && serialized.TryGetProperty("raw", out var raw))
            {
                JsonElement.DeepEquals(JsonDocument.Parse(WireJsonForPlatform(response)).RootElement, raw).Should().BeTrue("responses must propagate through both wrappers without losing their raw payload");
            }
            if (result is bool started)
                started.Should().Be(JsonDocument.Parse(WireJsonForPlatform(response)).RootElement.GetProperty("started").GetBoolean());
        }, expected, response);

    private static void AssertSubset(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            actual.ValueKind.Should().Be(JsonValueKind.Object);
            foreach (var property in expected.EnumerateObject())
            {
                actual.TryGetProperty(property.Name, out var value).Should().BeTrue($"request must include {property.Name}");
                JsonElement.DeepEquals(property.Value, value).Should().BeTrue($"{property.Name} should be {property.Value}, received {value}");
            }
        }
        else JsonElement.DeepEquals(expected, actual).Should().BeTrue();
    }

    // Keep the readable wire fixtures portable for APIs that validate local absolute paths.
    private static string PathForPlatform(string path) => OperatingSystem.IsWindows()
        ? JKToolKit.CodexSDK.Tests.TestHelpers.XPaths.Abs(path.TrimStart('/'))
        : path;

    private static string WireJsonForPlatform(string json)
    {
        if (!OperatingSystem.IsWindows()) return json;
        return Rewrite(JsonNode.Parse(json))?.ToJsonString() ?? "null";

        static JsonNode? Rewrite(JsonNode? node) => node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) && text.StartsWith('/') => JsonValue.Create(PathForPlatform(text)),
            JsonObject obj => new JsonObject(obj.Select(entry => new KeyValuePair<string, JsonNode?>(entry.Key, Rewrite(entry.Value))).ToArray()),
            JsonArray array => new JsonArray(array.Select(Rewrite).ToArray()),
            _ => node?.DeepClone()
        };
    }

    private sealed record Reply(int Value);

    private sealed class Factory(RecordingRpc rpc, bool experimentalApi = true) : ICodexAppServerClientFactory
    {
        public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default) => Task.FromResult(new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = experimentalApi }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false));
    }

    private sealed class Process : IStdioProcess
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => _completion.Task;
        public int? ProcessId => 123;
        public int? ExitCode => null;
        public IReadOnlyList<string> StderrTail => [];
        public ValueTask DisposeAsync() { _completion.TrySetResult(); return ValueTask.CompletedTask; }
    }

    private sealed class RecordingRpc(string response) : IJsonRpcConnection
    {
        public List<(string Method, JsonElement Parameters)> Requests { get; } = [];
        public Exception? Failure { get; set; }
        public Func<string, JsonElement>? Respond { get; set; }
        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }
        public Func<Task>? DisposeOverride { get; set; }
#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067
        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }
        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Requests.Add((method, JsonSerializer.SerializeToElement(@params, CodexAppServerClient.CreateDefaultSerializerOptions())));
            return Failure is null ? Task.FromResult(JsonDocument.Parse(WireJsonForPlatform((Respond?.Invoke(method))?.GetRawText() ?? response)).RootElement.Clone()) : Task.FromException<JsonElement>(Failure);
        }
        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public async ValueTask DisposeAsync() { DisposeCount++; if (DisposeOverride is not null) await DisposeOverride(); Disposed = true; }
        public ValueTask EmitAsync(string method, JsonElement? parameters) => OnNotification?.Invoke(new JsonRpcNotification(method, parameters)) ?? ValueTask.CompletedTask;
    }
}
