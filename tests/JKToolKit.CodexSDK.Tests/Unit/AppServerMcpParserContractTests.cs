using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerMcpParserContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData(null, McpAuthStatus.Unknown)]
    [InlineData(" ", McpAuthStatus.Unknown)]
    [InlineData("future", McpAuthStatus.Unknown)]
    [InlineData("unsupported", McpAuthStatus.Unsupported)]
    [InlineData("notLoggedIn", McpAuthStatus.NotLoggedIn)]
    [InlineData("bearerToken", McpAuthStatus.BearerToken)]
    [InlineData("oAuth", McpAuthStatus.OAuth)]
    [InlineData(" oauth ", McpAuthStatus.OAuth)]
    public void StatusList_ParsesAuthStatusAliases(string? status, McpAuthStatus expected)
    {
        var page = CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(JsonSerializer.SerializeToElement(new { data = new[] { new { name = "server", auth_status = status } } }));
        page.Servers.Should().ContainSingle().Which.AuthStatus.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, McpServerRuntimeStatus.Unknown)]
    [InlineData(" ", McpServerRuntimeStatus.Unknown)]
    [InlineData("future", McpServerRuntimeStatus.Unknown)]
    [InlineData("notStarted", McpServerRuntimeStatus.NotStarted)]
    [InlineData("starting", McpServerRuntimeStatus.Starting)]
    [InlineData(" connected ", McpServerRuntimeStatus.Connected)]
    [InlineData("authenticationRequired", McpServerRuntimeStatus.AuthenticationRequired)]
    [InlineData("failed", McpServerRuntimeStatus.Failed)]
    [InlineData("cancelled", McpServerRuntimeStatus.Cancelled)]
    [InlineData("disabled", McpServerRuntimeStatus.Disabled)]
    public void StatusList_ParsesRuntimeStatusAliases(string? status, McpServerRuntimeStatus expected)
    {
        var page = CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(JsonSerializer.SerializeToElement(new { data = new[] { new { name = "server", runtime_status = status } } }));
        page.Servers.Should().ContainSingle().Which.RuntimeStatus.Should().Be(expected);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":[null,42,{}, {\"name\":\" \"}]}")]
    public void StatusList_SkipsMalformedServers(string json)
    {
        var page = CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(Json(json));
        page.Servers.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
        page.Raw.GetRawText().Should().Be(json);
    }

    [Fact]
    public void StatusList_ParsesMetadataAndSkipsMalformedNestedEntries()
    {
        var page = CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(Json("""
            {"next_cursor":"next","data":[{"name":"server","plugin_id":"plug","http_origin":"https://example.test",
            "status":"failed","error":"failure","failureReason":"future","tools_error":"tool failure",
            "serverInfo":{"name":"impl","version":"1","title":"Title","description":"Description","website_url":"https://impl.test"},
            "serverCapabilities":{"resources":{}},
            "tools":{"invalid":null,"blank":{"name":" ","inputSchema":{}},"missing":{},
              "lookup":{"title":"Lookup","description":"Find","input_schema":{"type":"object"},"output_schema":{"type":"string"}}},
            "resources":[null,{}, {"name":"missingUri"},{"uri":"res://missingName"},
              {"name":"res","uri":"res://a","title":"Resource","description":"Read","mime_type":"text/plain","size":9223372036854775807}],
            "resource_templates":[false,{}, {"name":"missingUri"},{"uriTemplate":"res://{id}"},
              {"name":"template","uri_template":"res://{id}","title":"Template","description":"Expand","mime_type":"text/plain"}]}]}
            """));
        page.NextCursor.Should().Be("next");
        var server = page.Servers.Should().ContainSingle().Subject;
        server.Name.Should().Be("server");
        server.PluginId.Should().Be("plug");
        server.HttpOrigin.Should().Be("https://example.test");
        server.StartupStatus.Should().Be("failed");
        server.Error.Should().Be("failure");
        server.FailureReason.Should().NotBeNull("unknown future reason values remain forward compatible");
        server.ToolsError.Should().Be("tool failure");
        server.ServerCapabilities!.Value.GetProperty("resources").ValueKind.Should().Be(JsonValueKind.Object);
        var info = server.ServerInfo!;
        info.Name.Should().Be("impl"); info.Version.Should().Be("1"); info.Title.Should().Be("Title");
        info.Description.Should().Be("Description"); info.WebsiteUrl.Should().Be("https://impl.test");
        info.Raw.GetProperty("name").GetString().Should().Be("impl");
        var tool = server.Tools.Should().ContainSingle().Subject;
        tool.Name.Should().Be("lookup"); tool.Title.Should().Be("Lookup"); tool.Description.Should().Be("Find");
        tool.InputSchema!.Value.GetProperty("type").GetString().Should().Be("object");
        tool.OutputSchema!.Value.GetProperty("type").GetString().Should().Be("string");
        var resource = server.Resources.Should().ContainSingle().Subject;
        resource.Name.Should().Be("res"); resource.Uri.Should().Be("res://a"); resource.Title.Should().Be("Resource");
        resource.Description.Should().Be("Read"); resource.MimeType.Should().Be("text/plain"); resource.Size.Should().Be(long.MaxValue);
        var template = server.ResourceTemplates.Should().ContainSingle().Subject;
        template.Name.Should().Be("template"); template.UriTemplate.Should().Be("res://{id}");
        template.Title.Should().Be("Template"); template.Description.Should().Be("Expand"); template.MimeType.Should().Be("text/plain");
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"size\":null")]
    [InlineData(",\"size\":\"12\"")]
    [InlineData(",\"size\":1.5")]
    [InlineData(",\"size\":9223372036854775808")]
    public void StatusList_InvalidResourceSizeIsUnknown(string fields)
    {
        var page = CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(Json("{\"data\":[{\"name\":\"server\",\"resources\":[{\"name\":\"r\",\"uri\":\"r://a\"" + fields + "}]}]}"));
        page.Servers[0].Resources[0].Size.Should().BeNull();
    }

    [Theory]
    [InlineData("{}", "contents array")]
    [InlineData("{\"contents\":{}}", "contents array")]
    [InlineData("{\"contents\":[null]}", "only objects")]
    [InlineData("{\"contents\":[{}]}", "non-empty uri")]
    [InlineData("{\"contents\":[{\"uri\":\" \"}]}", "non-empty uri")]
    [InlineData("{\"contents\":[{\"uri\":\"a\"}]}", "exactly one")]
    [InlineData("{\"contents\":[{\"uri\":\"a\",\"text\":\"t\",\"blob\":\"AA==\"}]}", "exactly one")]
    [InlineData("{\"contents\":[{\"uri\":\"a\",\"text\":null,\"blob\":123}]}", "exactly one")]
    public void ResourceRead_RejectsMalformedResponseWithSpecificError(string json, string error)
    {
        Action act = () => CodexAppServerClientMcpParsers.ParseMcpResourceReadResult(Json(json));
        act.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Fact]
    public void ResourceRead_MapsTextAndBlobAndClonesContent()
    {
        McpResourceReadResult result;
        using (var document = JsonDocument.Parse("""{"origin_call_id":"call","contents":[{"uri":"res://text","text":"hello","mime_type":"text/plain"},{"uri":"res://blob","blob":"AA==","mimeType":"image/png"}]}"""))
            result = CodexAppServerClientMcpParsers.ParseMcpResourceReadResult(document.RootElement);
        result.OriginCallId.Should().Be("call");
        result.Contents.Should().HaveCount(2);
        result.Contents[0].Uri.Should().Be("res://text"); result.Contents[0].Text.Should().Be("hello");
        result.Contents[0].BlobBase64.Should().BeNull(); result.Contents[0].MimeType.Should().Be("text/plain");
        result.Contents[1].Uri.Should().Be("res://blob"); result.Contents[1].Text.Should().BeNull();
        result.Contents[1].BlobBase64.Should().Be("AA=="); result.Contents[1].MimeType.Should().Be("image/png");
        result.Contents[0].Raw.GetProperty("text").GetString().Should().Be("hello");
        result.Contents[1].Raw.GetProperty("blob").GetString().Should().Be("AA==");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"authorizationUrl\":\" \"}")]
    [InlineData("{\"authorizationUrl\":42}")]
    public void OAuth_MissingUrlIncludesRawResultInFailure(string json)
    {
        Action act = () => CodexAppServerClientMcpParsers.ParseMcpServerOauthLoginResult(Json(json));
        act.Should().Throw<InvalidOperationException>().WithMessage("*no authorizationUrl*" + json);
    }

    [Theory]
    [InlineData("authorizationUrl")]
    [InlineData("authorization_url")]
    public void OAuth_ParsesBothUrlSpellings(string field)
    {
        var result = CodexAppServerClientMcpParsers.ParseMcpServerOauthLoginResult(Json("{\"" + field + "\":\"https://auth.test\",\"loginId\":\"login-1\"}"));
        result.AuthorizationUrl.Should().Be("https://auth.test");
        result.LoginId.Should().Be("login-1");
        result.Raw.GetProperty(field).GetString().Should().Be("https://auth.test");
    }
}
