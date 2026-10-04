using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Theory]
    [InlineData("enablement", "{}") ]
    [InlineData("enablement", "{\"enablement\":null}") ]
    [InlineData("model", "{\"data\":[42]}")]
    [InlineData("feature", "{\"data\":[42]}")]
    [InlineData("write", "{\"status\":\"future\",\"version\":\"v\",\"filePath\":\"/config\"}")]
    [InlineData("write", "{\"status\":\"ok\",\"version\":\"v\",\"filePath\":\"/config\",\"overriddenMetadata\":{\"message\":\"overridden\"}}")]
    [InlineData("write", "{\"status\":\"ok\",\"version\":\"v\",\"filePath\":\"/config\",\"overriddenMetadata\":{\"message\":\"overridden\",\"overridingLayer\":{}}}")]
    [InlineData("usage", "{\"summary\":{},\"dailyUsageBuckets\":[42]}")]
    [InlineData("messages", "{\"featureEnabled\":true,\"messages\":[42]}")]
    [InlineData("environment", "[]")]
    [InlineData("watch", "{\"path\":\"relative\"}")]
    [InlineData("directory", "{}") ]
    [InlineData("directory", "[]") ]
    [InlineData("directory", "{\"entries\":null}") ]
    [InlineData("model", "{\"data\":[{\"id\":\"m\",\"model\":\"m\",\"displayName\":\"Model\",\"description\":\"description\",\"defaultReasoningEffort\":\"low\"}]}") ]
    [InlineData("directory", "{\"entries\":[42]}")]
    [InlineData("sandbox", "{}")]
    public async Task OperationResponses_RejectInvalidShapes(string operation, string response)
    {
        var rpc = new RecordingRpc(response);
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        Func<Task> action = operation switch
        {
            "enablement" => () => client.SetExperimentalFeatureEnablementAsync(new() { Enablement = new Dictionary<string, bool> { ["feature"] = true } }),
            "model" => () => client.ListModelsAsync(),
            "feature" => () => client.ListExperimentalFeaturesAsync(),
            "write" => () => client.WriteConfigValueAsync(ValidWrite()),
            "usage" => () => client.ReadAccountTokenUsageAsync(),
            "messages" => () => client.ReadWorkspaceMessagesAsync(),
            "environment" => () => client.AddEnvironmentAsync(new() { EnvironmentId = "env", ExecServerUrl = "https://example.test" }),
            "watch" => () => client.FsWatchAsync(new() { Path = PathForPlatform("/workspace") }),
            "directory" => () => client.FsReadDirectoryAsync(new() { Path = PathForPlatform("/workspace") }),
            _ => () => client.StartWindowsSandboxSetupAsync("elevated")
        };
        await action.Should().ThrowAsync<InvalidOperationException>();
        rpc.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(" ", "1", 0)]
    [InlineData("key", null, 0)]
    [InlineData("key", "1", 9)]
    public async Task ConfigValueWrite_RejectsInvalidKeyValueAndStrategy(string key, string? value, int strategy)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.WriteConfigValueAsync(new() { KeyPath = key, Value = value is null ? default : JsonDocument.Parse(value).RootElement.Clone(), MergeStrategy = (ConfigMergeStrategy)strategy });
        await action.Should().ThrowAsync<ArgumentException>();
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null-edit")]
    [InlineData("blank-key")]
    [InlineData("undefined-value")]
    public async Task ConfigBatchWrite_RejectsMalformedEditsBeforeDispatch(string kind)
    {
        IReadOnlyList<ConfigEditOperation> edits = kind switch
        {
            "empty" => [],
            "null-edit" => [null!],
            "blank-key" => [new() { KeyPath = " ", Value = JsonSerializer.SerializeToElement(1), MergeStrategy = ConfigMergeStrategy.Replace }],
            _ => [new() { KeyPath = "key", Value = default, MergeStrategy = ConfigMergeStrategy.Replace }]
        };
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.WriteConfigBatchAsync(new() { Edits = edits });
        await action.Should().ThrowAsync<ArgumentException>();
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"model\":\" \"}")]
    public async Task ModelCatalog_IgnoresUpgradeMetadataWithoutTarget(string upgradeInfo)
    {
        var response = """{"data":[{"id":"model-id","model":"model","displayName":"Model","description":"description","defaultReasoningEffort":"high","supportedReasoningEfforts":[],"upgradeInfo":UPGRADE}]}""".Replace("UPGRADE", upgradeInfo);
        var rpc = new RecordingRpc(response);
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ListModelsAsync();
        result.Data.Should().ContainSingle().Which.UpgradeInfo.Should().BeNull();
        result.Data[0].InputModalities.Should().Equal("text", "image");
        result.Data[0].SupportedReasoningEfforts.Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationSummary_ByRolloutPath_PreservesSummaryWithoutGitInfo()
    {
        var rpc = new RecordingRpc("""{"summary":{"conversationId":"conversation-1","path":"/rollout.jsonl","preview":"hello","modelProvider":"provider","cwd":"/workspace","cliVersion":"1","source":"cli"}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.GetConversationSummaryAsync(new() { RolloutPath = PathForPlatform("/rollout.jsonl") });
        result.Summary.ConversationId.Should().Be("conversation-1");
        result.Summary.GitInfo.Should().BeNull();
        result.Summary.Preview.Should().Be("hello");
        rpc.Requests.Should().ContainSingle().Which.Method.Should().Be("getConversationSummary");
        rpc.Requests[0].Parameters.GetProperty("rolloutPath").GetString().Should().Be(PathForPlatform("/rollout.jsonl"));
        rpc.Requests[0].Parameters.TryGetProperty("conversationId", out _).Should().BeFalse();
    }

    private static ConfigValueWriteOptions ValidWrite() => new() { KeyPath = "key", Value = JsonSerializer.SerializeToElement(1), MergeStrategy = ConfigMergeStrategy.Replace };
}
