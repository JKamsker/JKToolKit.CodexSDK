using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerConfigBuilderContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Builder_RejectsMissingNamesBeforeModifyingOverrides(string? blank)
    {
        var builder = new CodexConfigOverridesBuilder();
        var invalid = new (Action Call, string Parameter)[]
        {
            (() => builder.Set(blank!, true), "dottedPath"),
            (() => builder.SetMcpServerStdio(blank!, "cmd"), "name"),
            (() => builder.SetMcpServerStdio("name", blank!), "command"),
            (() => builder.SetMcpServerStreamableHttp(blank!, "https://test"), "name"),
            (() => builder.SetMcpServerStreamableHttp("name", blank!), "url"),
            (() => builder.SetMcpServerTool(blank!, "tool"), "serverName"),
            (() => builder.SetMcpServerTool("name", blank!), "toolName")
        };
        foreach (var (call, parameter) in invalid)
            call.Should().Throw<ArgumentException>().WithParameterName(parameter).WithMessage("*cannot be empty or whitespace*");
        builder.BuildOrNull().Should().BeNull(); builder.Build().EnumerateObject().Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    public void Builder_RejectsExplicitEmptyOptionalCredentialsAndToolPolicy(string blank)
    {
        var builder = new CodexConfigOverridesBuilder();
        Action id = () => builder.SetMcpServerStreamableHttp("name", "https://test", oauthClientId: blank);
        Action secret = () => builder.SetMcpServerStreamableHttp("name", "https://test", oauthClientId: "client", oauthClientSecret: blank);
        Action mode = () => builder.SetMcpServerTool("name", "tool", approvalMode: blank);
        id.Should().Throw<ArgumentException>().WithParameterName("oauthClientId").WithMessage("*OAuth client ID*");
        secret.Should().Throw<ArgumentException>().WithParameterName("oauthClientSecret").WithMessage("*OAuth client secret*");
        mode.Should().Throw<ArgumentException>().WithParameterName("approvalMode").WithMessage("*Approval mode*");
        builder.BuildOrNull().Should().BeNull();
    }

    [Fact]
    public void StdioOverrides_SnapshotCollectionsAndRemoveOmittedSettingsOnReplacement()
    {
        var args = new List<string> { "arg" }; var vars = new List<string> { "ENV" }; var enabledTools = new List<string> { "enabled" };
        var disabledTools = new List<string> { "disabled" }; var scopes = new List<string> { "scope" }; var env = new Dictionary<string,string> { ["Name"] = "Value" };
        var builder = new CodexConfigOverridesBuilder();
        builder.SetMcpServerStdio("local", "command", args, env, vars, "/cwd", false, true, 12, 34, enabledTools, disabledTools, scopes).Should().BeSameAs(builder);
        args.Clear(); vars.Clear(); enabledTools.Clear(); disabledTools.Clear(); scopes.Clear(); env.Clear();
        var expected = JsonSerializer.Deserialize<JsonElement>("""
            {"mcp_servers.local.command":"command","mcp_servers.local.args":["arg"],"mcp_servers.local.env":{"Name":"Value"},
            "mcp_servers.local.env_vars":["ENV"],"mcp_servers.local.cwd":"/cwd","mcp_servers.local.enabled":false,"mcp_servers.local.required":true,
            "mcp_servers.local.startup_timeout_sec":12,"mcp_servers.local.tool_timeout_sec":34,"mcp_servers.local.enabled_tools":["enabled"],
            "mcp_servers.local.disabled_tools":["disabled"],"mcp_servers.local.scopes":["scope"]}
            """);
        var snapshot = builder.BuildOrNull()!.Value;
        JsonElement.DeepEquals(snapshot, expected).Should().BeTrue();
        builder.SetMcpServerStdio("local", "replacement");
        builder.Build().EnumerateObject().Should().ContainSingle().Which.Name.Should().Be("mcp_servers.local.command");
        builder.Build().GetProperty("mcp_servers.local.command").GetString().Should().Be("replacement");
        JsonElement.DeepEquals(snapshot, expected).Should().BeTrue("previously built JSON must remain immutable");
    }

    [Fact]
    public void HttpOverrides_SnapshotHeadersAndCollectionsAndRemoveOptionalCredentialsOnReplacement()
    {
        var headers = new Dictionary<string,string> { ["Header"] = "value" }; var envHeaders = new Dictionary<string,string> { ["Authorization"] = "TOKEN" };
        var enabled = new List<string> { "read" }; var disabled = new List<string> { "write" }; var scopes = new List<string> { "scope" };
        var builder = new CodexConfigOverridesBuilder();
        builder.SetMcpServerStreamableHttp("http", "https://test", "BEARER", headers, envHeaders, false, true, 12, 34, enabled, disabled, scopes, "client", "secret").Should().BeSameAs(builder);
        headers.Clear(); envHeaders.Clear(); enabled.Clear(); disabled.Clear(); scopes.Clear();
        var expected = JsonSerializer.Deserialize<JsonElement>("""
            {"mcp_servers.http.url":"https://test","mcp_servers.http.bearer_token_env_var":"BEARER","mcp_servers.http.http_headers":{"Header":"value"},
            "mcp_servers.http.env_http_headers":{"Authorization":"TOKEN"},"mcp_servers.http.enabled":false,"mcp_servers.http.required":true,
            "mcp_servers.http.startup_timeout_sec":12,"mcp_servers.http.tool_timeout_sec":34,"mcp_servers.http.enabled_tools":["read"],
            "mcp_servers.http.disabled_tools":["write"],"mcp_servers.http.scopes":["scope"],"mcp_servers.http.oauth.client_id":"client","mcp_servers.http.oauth.client_secret":"secret"}
            """);
        JsonElement.DeepEquals(builder.Build(), expected).Should().BeTrue();
        builder.SetMcpServerStreamableHttp("http", "https://replacement");
        builder.Build().EnumerateObject().Should().ContainSingle().Which.Name.Should().Be("mcp_servers.http.url");
        builder.Build().GetProperty("mcp_servers.http.url").GetString().Should().Be("https://replacement");
        builder.Set("mcp_servers.http.url", null).Should().BeSameAs(builder); builder.BuildOrNull().Should().BeNull();
    }

    [Fact]
    public void ToolOverrides_DistinguishMinimumLimitFromRemovalAndDoNotAffectOtherKeys()
    {
        var builder = new CodexConfigOverridesBuilder().Set("model", "chosen");
        builder.SetMcpServerTool("server", "tool", "never", 1).Should().BeSameAs(builder);
        builder.Build().GetProperty("mcp_servers.server.tools.tool.output_token_limit").GetInt64().Should().Be(1);
        builder.Build().GetProperty("mcp_servers.server.tools.tool.approval_mode").GetString().Should().Be("never");
        builder.SetMcpServerTool("server", "tool");
        builder.Build().EnumerateObject().Should().ContainSingle().Which.Name.Should().Be("model");
        builder.Build().GetProperty("model").GetString().Should().Be("chosen");
        foreach (var invalid in new long[] { -1, 0 })
        {
            Action update = () => builder.SetMcpServerTool("server", "tool", outputTokenLimit: invalid);
            update.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("outputTokenLimit").Which.ActualValue.Should().Be(invalid);
        }
    }
    [Fact]
    public void HttpOverrides_ExplainWhyAClientSecretRequiresAClientId()
    {
        Action create = () => new CodexConfigOverridesBuilder().SetMcpServerStreamableHttp("server", "https://test", oauthClientSecret: "secret");
        create.Should().Throw<ArgumentException>().WithParameterName("oauthClientId").WithMessage("*OAuth client ID is required*OAuth client secret*");
    }

}
