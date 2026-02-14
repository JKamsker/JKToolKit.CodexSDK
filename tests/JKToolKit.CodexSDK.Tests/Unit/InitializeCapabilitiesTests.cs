using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class InitializeCapabilitiesTests
{
    [Fact]
    public void NormalizeCapabilities_ReturnsNull_WhenNull()
    {
        CodexAppServerClient.NormalizeCapabilities(null).Should().BeNull();
    }

    [Fact]
    public void NormalizeCapabilities_ReturnsNull_WhenEmpty()
    {
        CodexAppServerClient.NormalizeCapabilities(new InitializeCapabilities()).Should().BeNull();
    }

    [Fact]
    public void NormalizeCapabilities_ReturnsNull_WhenOptOutEmpty()
    {
        CodexAppServerClient.NormalizeCapabilities(new InitializeCapabilities
        {
            OptOutNotificationMethods = Array.Empty<string>()
        }).Should().BeNull();
    }

    [Fact]
    public void BuildInitializeParams_NormalizesEmptyCapabilities_ToNull()
    {
        var p = CodexAppServerClient.BuildInitializeParams(
            new AppServerClientInfo("id", "name", "1"),
            new InitializeCapabilities());

        p.Capabilities.Should().BeNull();
    }

    [Fact]
    public void NormalizeCapabilities_PreservesExperimentalApi_AndOptOut()
    {
        var normalized = CodexAppServerClient.NormalizeCapabilities(new InitializeCapabilities
        {
            ExperimentalApi = true,
            OptOutNotificationMethods = ["item/agentMessage/delta"]
        });

        normalized.Should().NotBeNull();
        normalized!.ExperimentalApi.Should().BeTrue();
        normalized.OptOutNotificationMethods.Should().Equal("item/agentMessage/delta");
    }

    [Fact]
    public void InitializeParams_Serialization_OmitsCapabilities_WhenNull()
    {
        var json = JsonSerializer.Serialize(
            new InitializeParams
            {
                ClientInfo = new AppServerClientInfo("id", "name", "1"),
                Capabilities = null
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().NotContain("capabilities");
    }

    [Fact]
    public void InitializeParams_Serialization_IncludesExperimentalApi_WhenEnabled()
    {
        var json = JsonSerializer.Serialize(
            new InitializeParams
            {
                ClientInfo = new AppServerClientInfo("id", "name", "1"),
                Capabilities = new InitializeCapabilities { ExperimentalApi = true }
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"capabilities\":");
        json.Should().Contain("\"experimentalApi\":true");
    }

    [Fact]
    public void InitializeParams_Serialization_OmitsExperimentalApi_WhenFalse_AndIncludesOptOut()
    {
        var json = JsonSerializer.Serialize(
            new InitializeParams
            {
                ClientInfo = new AppServerClientInfo("id", "name", "1"),
                Capabilities = new InitializeCapabilities { OptOutNotificationMethods = ["x"] }
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"capabilities\":");
        json.Should().Contain("\"optOutNotificationMethods\":[\"x\"]");
        json.Should().NotContain("experimentalApi");
    }
}
