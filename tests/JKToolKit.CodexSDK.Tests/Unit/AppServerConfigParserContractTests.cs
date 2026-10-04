using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerConfigParserContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("-1")]
    [InlineData("\"-1\"")]
    [InlineData("\"bad\"")]
    [InlineData("1e300")]
    [InlineData("1e400")]
    [InlineData("\"Infinity\"")]
    [InlineData("\"NaN\"")]
    [InlineData("922337203686")]
    public void InvalidTimeout_IsIgnoredInsteadOfBreakingConfigRead(string timeout)
    {
        var result = CodexAppServerClientConfigReadParsers.ParseConfigReadResult(Json("{\"mcp_servers\":{\"server\":{\"startup_timeout_sec\":" + timeout + ",\"tool_timeout_sec\":" + timeout + "}}}"));
        result.McpServers!["server"].StartupTimeout.Should().BeNull();
        result.McpServers["server"].ToolTimeout.Should().BeNull();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void NumericStringTimeout_UsesProtocolInvariantCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = CodexAppServerClientConfigReadParsers.ParseConfigReadResult(Json("""{"mcp_servers":{"s":{"startupTimeoutSec":"1.5","toolTimeoutSec":2.25}}}"""));
            result.McpServers!["s"].StartupTimeout.Should().Be(TimeSpan.FromSeconds(1.5));
            result.McpServers["s"].ToolTimeout.Should().Be(TimeSpan.FromSeconds(2.25));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"layers\":null,\"mcp_servers\":null}")]
    [InlineData("{\"layers\":false,\"mcp_servers\":[]}")]
    public void MissingOrWrongKindOptionalSectionsAreAbsent(string json)
    {
        var result = CodexAppServerClientConfigReadParsers.ParseConfigReadResult(Json(json));
        result.Layers.Should().BeNull(); result.Origins.Should().BeNull(); result.McpServers.Should().BeNull();
        result.Config.GetRawText().Should().Be(json); result.Raw.GetRawText().Should().Be(json);
    }

    [Fact]
    public void Layers_FilterMalformedEntriesAndRetainUnknownSourceFields()
    {
        var result = CodexAppServerClientConfigReadParsers.ParseConfigReadResult(Json("""
            {"config":{},"layers":[null,{}, {"name":{},"version":" "},
              {"name":{"id":"id","name":"name","domain":"domain","key":"key","file":"file","profile":"profile","dot_codex_folder":"folder"},"version":"v","disabled_reason":"disabled"}],
             "origins":{"skip":42,"model":{"name":{"type":"user"},"version":"v2"}}}
            """));
        result.Config.GetRawText().Should().Be("{}");
        var layer = result.Layers.Should().ContainSingle().Subject;
        layer.Version.Should().Be("v"); layer.DisabledReason.Should().Be("disabled"); layer.Config.ValueKind.Should().Be(JsonValueKind.Undefined);
        layer.Name.Type.Should().Be("unknown"); layer.Name.Id.Should().Be("id"); layer.Name.Name.Should().Be("name");
        layer.Name.Domain.Should().Be("domain"); layer.Name.Key.Should().Be("key"); layer.Name.File.Should().Be("file");
        layer.Name.Profile.Should().Be("profile"); layer.Name.DotCodexFolder.Should().Be("folder");
        result.Origins.Should().ContainSingle(); result.Origins!["model"].Version.Should().Be("v2");
        result.Origins["model"].Name.Type.Should().Be("user");
    }

    [Theory]
    [InlineData("{}", "name")]
    [InlineData("{\"name\":{}}", "version")]
    [InlineData("{\"name\":{},\"version\":\" \"}", "version")]
    public void Metadata_RequiresNameAndVersionAndExplainsContext(string json, string field)
    {
        Action parse = () => CodexAppServerClientConfigReadParsers.ParseConfigLayerMetadataInfo(Json(json), "origin model");
        parse.Should().Throw<InvalidOperationException>().WithMessage("*'" + field + "'*origin model*");
    }

    [Fact]
    public void McpConfig_MapsCamelCaseAliasesAndFiltersNonStringEnvironmentValues()
    {
        var result = CodexAppServerClientConfigReadParsers.ParseConfigReadResult(Json("""
            {"config":{"mcp_servers":{"skip":42,"stdio":{"command":"tool","url":"https://ignored.test","args":["--run"],"env":{"A":"value","B":42},"envVars":["C"],"cwd":"/repo",
              "bearerTokenEnvVar":"TOKEN","httpHeaders":{"X":"v"},"envHttpHeaders":{"Y":"ENV"},"enabled":true,"required":false,
              "startupTimeoutSec":0,"toolTimeoutSec":2,"enabledTools":["first"],"disabledTools":["second"],"scopes":["read"]},
              "http":{"url":"https://example.test"},"unknown":{}}}}
            """));
        result.McpServers.Should().HaveCount(3);
        var server = result.McpServers!["stdio"];
        server.Transport.Should().Be("stdio"); server.Command.Should().Be("tool"); server.Url.Should().Be("https://ignored.test");
        server.Args.Should().Equal("--run"); server.Env.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string,string>("A", "value"));
        server.EnvVars.Should().Equal("C"); server.Cwd.Should().Be("/repo"); server.BearerTokenEnvVar.Should().Be("TOKEN");
        server.HttpHeaders!["X"].Should().Be("v"); server.EnvHttpHeaders!["Y"].Should().Be("ENV");
        server.Enabled.Should().BeTrue(); server.Required.Should().BeFalse(); server.StartupTimeout.Should().Be(TimeSpan.Zero);
        server.ToolTimeout.Should().Be(TimeSpan.FromSeconds(2)); server.EnabledTools.Should().Equal("first"); server.DisabledTools.Should().Equal("second"); server.Scopes.Should().Equal("read");
        result.McpServers["http"].Transport.Should().Be("streamableHttp"); result.McpServers["unknown"].Transport.Should().Be("unknown");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void McpConfig_MalformedDictionariesAreAbsent(string dictionary)
    {
        var result = CodexAppServerClientConfigReadParsers.ParseConfigReadResult(Json("{\"mcp_servers\":{\"s\":{\"env\":" + dictionary + "}}}"));
        result.McpServers!["s"].Env.Should().BeNull();
    }
}
