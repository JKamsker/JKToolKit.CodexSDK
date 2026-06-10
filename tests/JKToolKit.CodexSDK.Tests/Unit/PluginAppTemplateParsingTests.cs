using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class PluginAppTemplateParsingTests
{
    [Fact]
    public async Task ReadPluginAsync_ParsesAppTemplates()
    {
        var marketplacePath = XPaths.JsonAbs("market");
        var sourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var doc = JsonDocument.Parse(
            $@"{{
              ""plugin"": {{
                ""description"": ""desc"",
                ""marketplaceName"": ""official"",
                ""marketplacePath"": ""{marketplacePath}"",
                ""skills"": [],
                ""summary"": {{
                  ""id"": ""plug-1"",
                  ""name"": ""Plugin One"",
                  ""installed"": true,
                  ""enabled"": true,
                  ""authPolicy"": ""ON_USE"",
                  ""installPolicy"": ""INSTALLED_BY_DEFAULT"",
                  ""source"": {{
                    ""type"": ""local"",
                    ""path"": ""{sourcePath}""
                  }}
                }},
                ""apps"": [],
                ""appTemplates"": [
                  {{
                    ""templateId"": ""template-1"",
                    ""name"": ""Issue Tracker"",
                    ""description"": ""Track issues"",
                    ""canonicalConnectorId"": ""connector-1"",
                    ""logoUrl"": ""https://example.test/logo.png"",
                    ""logoUrlDark"": ""https://example.test/logo-dark.png"",
                    ""materializedAppIds"": [""app-1""],
                    ""reason"": null
                  }},
                  {{
                    ""templateId"": ""template-2"",
                    ""name"": ""Build Monitor"",
                    ""description"": null,
                    ""canonicalConnectorId"": null,
                    ""logoUrl"": null,
                    ""logoUrlDark"": null,
                    ""materializedAppIds"": [],
                    ""reason"": ""NOT_CONFIGURED_FOR_WORKSPACE""
                  }}
                ],
                ""hooks"": [],
                ""mcpServers"": []
              }}
            }}");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        rpc.LastMethod.Should().Be("plugin/read");
        result.Plugin.AppTemplates.Should().HaveCount(2);

        var available = result.Plugin.AppTemplates[0];
        available.TemplateId.Should().Be("template-1");
        available.Name.Should().Be("Issue Tracker");
        available.CanonicalConnectorId.Should().Be("connector-1");
        available.LogoUrlDark.Should().Be("https://example.test/logo-dark.png");
        available.MaterializedAppIds.Should().Equal("app-1");
        available.Reason.Should().BeNull();
        available.ReasonValue.Should().BeNull();

        var unavailable = result.Plugin.AppTemplates[1];
        unavailable.TemplateId.Should().Be("template-2");
        unavailable.Reason.Should().Be("NOT_CONFIGURED_FOR_WORKSPACE");
        unavailable.ReasonValue.Should().Be(PluginAppTemplateUnavailableReason.NotConfiguredForWorkspace);
        unavailable.Raw.GetProperty("templateId").GetString().Should().Be("template-2");
    }

    private static CodexAppServerClient CreateClient(IJsonRpcConnection rpc) =>
        new(
            new CodexAppServerClientOptions(),
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

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
