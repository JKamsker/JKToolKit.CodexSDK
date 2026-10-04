using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SandboxPolicyWireContractTests
{
    public static IEnumerable<object[]> Policies()
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        yield return [new SandboxPolicy.DangerFullAccess(), """{"type":"dangerFullAccess"}"""];
        yield return [new SandboxPolicy.ExternalSandbox { NetworkAccess = SandboxNetworkAccess.Enabled }, """{"type":"externalSandbox","networkAccess":"enabled"}"""];
        yield return [new SandboxPolicy.ReadOnly { NetworkAccess = true, Access = new ReadOnlyAccess.FullAccess() }, """{"type":"readOnly","networkAccess":true,"access":{"type":"fullAccess"}}"""];
        yield return [new SandboxPolicy.WorkspaceWrite
        {
            WritableRoots = [root], NetworkAccess = true, ExcludeTmpdirEnvVar = true, ExcludeSlashTmp = true,
            ReadOnlyAccess = new ReadOnlyAccess.Restricted { IncludePlatformDefaults = false, ReadableRoots = [root] }
        }, JsonSerializer.Serialize(new
        {
            type = "workspaceWrite", writableRoots = new[] { root }, networkAccess = true,
            excludeTmpdirEnvVar = true, excludeSlashTmp = true,
            readOnlyAccess = new { type = "restricted", includePlatformDefaults = false, readableRoots = new[] { root } }
        })];
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public void TurnStartParameters_PreserveAllSandboxVariantFields(SandboxPolicy policy, string expectedWire)
    {
        var request = new TurnStartParams { ThreadId = "thread-1", Input = [], SandboxPolicy = policy };
        var json = JsonSerializer.SerializeToElement(request, CodexAppServerClient.CreateDefaultSerializerOptions());
        JsonElement.DeepEquals(json.GetProperty("sandboxPolicy"), JsonSerializer.Deserialize<JsonElement>(expectedWire)).Should().BeTrue();
    }
    [Theory]
    [MemberData(nameof(Policies))]
    public void BaseTypedPolicy_RoundTripsVariantFieldsAndRuntimeType(SandboxPolicy policy, string wire)
    {
        foreach (var options in new[] { new JsonSerializerOptions(), CodexAppServerClient.CreateDefaultSerializerOptions() })
        {
            var restored = JsonSerializer.Deserialize<SandboxPolicy>(wire, options);
            restored.Should().BeOfType(policy.GetType());
            JsonElement.DeepEquals(JsonSerializer.SerializeToElement(restored, options), JsonSerializer.Deserialize<JsonElement>(wire)).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("{\"type\":42}")]
    [InlineData("{\"type\":\"futureSandbox\"}")]
    public void UnknownOrMalformedDiscriminators_AreRejected(string wire) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SandboxPolicy>(wire));

    [Fact]
    public void NullPolicy_RemainsNull()
    {
        JsonSerializer.Deserialize<SandboxPolicy>("null").Should().BeNull();
        JsonSerializer.Serialize<SandboxPolicy?>(null).Should().Be("null");
    }

    [Fact]
    public void Reader_AllowsDiscriminatorAfterVariantFields()
    {
        var policy = JsonSerializer.Deserialize<SandboxPolicy>("""{"networkAccess":true,"access":{"type":"fullAccess"},"type":"readOnly"}""")
            .Should().BeOfType<SandboxPolicy.ReadOnly>().Subject;
        policy.NetworkAccess.Should().BeTrue();
        policy.Access.Should().BeOfType<ReadOnlyAccess.FullAccess>();
    }

    [Fact]
    public void Writer_PreservesCustomPoliciesAndSerializerOptions()
    {
        SandboxPolicy policy = new FutureSandboxPolicy { AuditWrites = true };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var wire = JsonSerializer.SerializeToElement(policy, options);
        JsonElement.DeepEquals(wire, JsonSerializer.Deserialize<JsonElement>("""{"type":"future","audit_writes":true}""")).Should().BeTrue();
    }

    private sealed record FutureSandboxPolicy : SandboxPolicy
    {
        public override string Type => "future";
        public bool AuditWrites { get; init; }
        public string? OptionalLabel { get; init; }
    }
}
