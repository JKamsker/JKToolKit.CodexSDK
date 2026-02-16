using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpServerParamsSerializationTests
{
    [Fact]
    public void Serialize_ListMcpServerStatusParams_UsesCursorAndLimit()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(
            new ListMcpServerStatusParams { Cursor = "10", Limit = 25 },
            options);

        json.Should().Contain("\"cursor\":\"10\"");
        json.Should().Contain("\"limit\":25");
    }

    [Fact]
    public void Serialize_McpServerOauthLoginParams_UsesTimeoutSecs()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(
            new McpServerOauthLoginParams { Name = "my-server", TimeoutSecs = 30 },
            options);

        json.Should().Contain("\"name\":\"my-server\"");
        json.Should().Contain("\"timeoutSecs\":30");
    }
}

