using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.Tests.TestHelpers;

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
                ReadableRoots = [XPaths.Abs("repo")]
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"restricted\"");
        json.Should().Contain("\"includePlatformDefaults\":true");
        json.Should().Contain($"\"readableRoots\":[\"{XPaths.JsonEsc("repo")}\"]");
    }

    [Fact]
    public void ReadOnlyAccess_Restricted_WithRelativeRoot_Throws()
    {
        var act = () => new ReadOnlyAccess.Restricted
        {
            ReadableRoots = ["relative\\repo"]
        };

        act.Should().Throw<ArgumentException>()
            .WithMessage("*absolute path*");
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
    public void SandboxPolicy_ReadOnly_IncludesNetworkAccess_WhenSet()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.ReadOnly { NetworkAccess = true },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"readOnly\"");
        json.Should().Contain("\"networkAccess\":true");
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

    [Fact]
    public void SandboxPolicy_WorkspaceWrite_WithRelativeWritableRoot_Throws()
    {
        var act = () => new SandboxPolicy.WorkspaceWrite
        {
            WritableRoots = ["relative\\repo"]
        };

        act.Should().Throw<ArgumentException>()
            .WithMessage("*absolute path*");
    }

    [Fact]
    public void SandboxPolicy_ExternalSandbox_OmitsNetworkAccess_WhenUnset()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.ExternalSandbox(),
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Be("{\"type\":\"externalSandbox\"}");
        json.Should().NotContain("\"networkAccess\"");
    }

    [Fact]
    public void SandboxPolicy_ExternalSandbox_IncludesTypedNetworkAccess_WhenSet()
    {
        var json = JsonSerializer.Serialize(
            new SandboxPolicy.ExternalSandbox { NetworkAccess = SandboxNetworkAccess.Enabled },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"externalSandbox\"");
        json.Should().Contain("\"networkAccess\":\"enabled\"");
    }
}

