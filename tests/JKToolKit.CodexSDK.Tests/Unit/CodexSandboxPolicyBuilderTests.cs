using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSandboxPolicyBuilderTests
{
    [Fact]
    public void ReadOnlyRestricted_BuildsAccessOverrideShape()
    {
        var policy = CodexSandboxPolicyBuilder.ReadOnlyRestricted(
            readableRoots: [XPaths.Abs("repo")],
            includePlatformDefaults: false);

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("readOnly");
        json.GetProperty("access").GetProperty("type").GetString().Should().Be("restricted");
        json.GetProperty("access").GetProperty("includePlatformDefaults").GetBoolean().Should().BeFalse();
        json.GetProperty("access").GetProperty("readableRoots").EnumerateArray().Select(x => x.GetString()).Should().Equal(XPaths.Abs("repo"));
    }

    [Fact]
    public void ReadOnly_WithNetworkAccess_BuildsShape()
    {
        var policy = CodexSandboxPolicyBuilder.ReadOnly(networkAccess: true);

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("readOnly");
        json.GetProperty("networkAccess").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void ExternalSandbox_Default_OmitsNetworkAccess()
    {
        var policy = CodexSandboxPolicyBuilder.ExternalSandbox();

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("externalSandbox");
        json.TryGetProperty("networkAccess", out _).Should().BeFalse();
    }

    [Fact]
    public void ExternalSandbox_WithNetworkAccess_BuildsShape()
    {
        var policy = CodexSandboxPolicyBuilder.ExternalSandbox(
            networkAccess: JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy.SandboxNetworkAccess.Enabled);

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("externalSandbox");
        json.GetProperty("networkAccess").GetString().Should().Be("enabled");
    }

    [Fact]
    public void WorkspaceWrite_WithReadOnlyAccess_BuildsShape()
    {
        var policy = CodexSandboxPolicyBuilder.WorkspaceWrite(
            writableRoots: [XPaths.Abs("repo")],
            networkAccess: true,
            readOnlyAccess: new JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy.ReadOnlyAccess.FullAccess());

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("workspaceWrite");
        json.GetProperty("networkAccess").GetBoolean().Should().BeTrue();
        json.GetProperty("writableRoots").EnumerateArray().Select(x => x.GetString()).Should().Equal(XPaths.Abs("repo"));
        json.GetProperty("readOnlyAccess").GetProperty("type").GetString().Should().Be("fullAccess");
    }
}
