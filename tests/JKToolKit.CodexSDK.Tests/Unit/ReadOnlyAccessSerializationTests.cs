using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ReadOnlyAccessSerializationTests
{
    [Fact]
    public void ReadOnlyAccess_FullAccess_Serializes_AsExpected()
    {
        var json = JsonSerializer.Serialize(
            new ReadOnlyAccess.FullAccess(),
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Be("{\"type\":\"fullAccess\"}");
    }

    [Fact]
    public void ReadOnlyAccess_Restricted_Serializes_AsExpected()
    {
        var json = JsonSerializer.Serialize(
            new ReadOnlyAccess.Restricted
            {
                IncludePlatformDefaults = true,
                ReadableRoots = ["C:\\repo"]
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"restricted\"");
        json.Should().Contain("\"includePlatformDefaults\":true");
        json.Should().Contain("\"readableRoots\":[\"C:\\\\repo\"]");
    }

    [Fact]
    public void SandboxPolicy_ReadOnly_OmitsAccess_WhenNull()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.ReadOnly(),
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Be("{\"type\":\"readOnly\"}");
        json.Should().NotContain("\"access\"");
    }

    [Fact]
    public void SandboxPolicy_ReadOnly_IncludesAccess_WhenSet()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.ReadOnly { Access = new ReadOnlyAccess.FullAccess() },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"readOnly\"");
        json.Should().Contain("\"access\":{\"type\":\"fullAccess\"}");
    }

    [Fact]
    public void SandboxPolicy_WorkspaceWrite_OmitsReadOnlyAccess_WhenNull()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.WorkspaceWrite(),
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().NotContain("\"readOnlyAccess\"");
    }

    [Fact]
    public void SandboxPolicy_WorkspaceWrite_IncludesReadOnlyAccess_WhenSet()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.WorkspaceWrite { ReadOnlyAccess = new ReadOnlyAccess.Restricted() },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"readOnlyAccess\":");
        json.Should().Contain("\"type\":\"restricted\"");
    }
}

