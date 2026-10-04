using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerModelBoundaryContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("notLoaded", CodexThreadStatusKind.NotLoaded)]
    [InlineData("idle", CodexThreadStatusKind.Idle)]
    [InlineData("systemError", CodexThreadStatusKind.SystemError)]
    [InlineData("active", CodexThreadStatusKind.Active)]
    [InlineData("ACTIVE", CodexThreadStatusKind.Unknown)]
    [InlineData("future", CodexThreadStatusKind.Unknown)]
    public void ThreadStatus_NormalizesOnlyKnownCaseSensitiveValuesAndPreservesRaw(string wire, CodexThreadStatusKind expected)
    {
        var raw = JsonSerializer.SerializeToElement(new { type = wire, activeFlags = new[] { "waitingOnApproval" } });
        var status = new CodexThreadStatus(wire, ["waitingOnApproval"], raw);
        status.Kind.Should().Be(expected); status.Type.Should().Be(wire); status.ActiveFlags.Should().Equal("waitingOnApproval");
        JsonElement.DeepEquals(status.Raw, raw).Should().BeTrue();
        Action invalid = () => new CodexThreadStatus(null!, null, raw);
        invalid.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    [Theory]
    [InlineData("completed", CodexTurnStatus.Completed)]
    [InlineData("interrupted", CodexTurnStatus.Interrupted)]
    [InlineData("failed", CodexTurnStatus.Failed)]
    [InlineData("inProgress", CodexTurnStatus.InProgress)]
    [InlineData("in_progress", CodexTurnStatus.InProgress)]
    [InlineData("future", CodexTurnStatus.Unknown)]
    [InlineData(null, CodexTurnStatus.Unknown)]
    public void TurnStatus_AcceptsLegacyAliasAndPreservesUnknownSemantics(string? wire, CodexTurnStatus expected) =>
        CodexTurnStatusExtensions.Parse(wire).Should().Be(expected);

    [Fact]
    public void ReviewTargets_EmitDistinctWireVariantsAndPreserveOpaqueFutureTargets()
    {
        var variants = new (ReviewTarget Target, string Expected)[]
        {
            (new ReviewTarget.UncommittedChanges(), """{"type":"uncommittedChanges"}"""),
            (new ReviewTarget.BaseBranch("main"), """{"type":"baseBranch","branch":"main"}"""),
            (new ReviewTarget.Commit("abc", "Title"), """{"type":"commit","sha":"abc","title":"Title"}"""),
            (new ReviewTarget.Commit("abc", null), """{"type":"commit","sha":"abc"}"""),
            (new ReviewTarget.Custom("Review carefully"), """{"type":"custom","instructions":"Review carefully"}"""),
            (new ReviewTarget.Raw(Json("""{"type":"future","criteria":[1,2]}""")), """{"type":"future","criteria":[1,2]}""")
        };
        foreach (var (target, expected) in variants)
            JsonElement.DeepEquals(target.ToWire(), Json(expected)).Should().BeTrue(target.GetType().Name);
        Action branch = () => new ReviewTarget.BaseBranch(null!); Action sha = () => new ReviewTarget.Commit(null!, null);
        Action instructions = () => new ReviewTarget.Custom(null!); Action undefined = () => new ReviewTarget.Raw(default);
        branch.Should().Throw<ArgumentNullException>().WithParameterName("branch"); sha.Should().Throw<ArgumentNullException>().WithParameterName("sha");
        instructions.Should().Throw<ArgumentNullException>().WithParameterName("instructions");
        undefined.Should().Throw<ArgumentException>().WithParameterName("wire").WithMessage("*must not be undefined*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void FullReadAccessBuilder_EmitsExplicitAccessAndIndependentNetworkOverride(bool? network)
    {
        var policy = CodexSandboxPolicyBuilder.ReadOnlyFullAccess(network);
        policy.Type.Should().Be("readOnly"); policy.Access.Should().BeOfType<ReadOnlyAccess.FullAccess>(); policy.NetworkAccess.Should().Be(network);
        var json = JsonSerializer.SerializeToElement(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        json.GetProperty("type").GetString().Should().Be("readOnly"); json.GetProperty("access").GetProperty("type").GetString().Should().Be("fullAccess");
        if (network.HasValue) json.GetProperty("networkAccess").GetBoolean().Should().Be(network.Value);
        else json.TryGetProperty("networkAccess", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void WebRtcTransport_RequiresAnOffer(string? sdp)
    {
        Action create = () => ThreadRealtimeTransport.WebRtc(sdp!);
        create.Should().Throw<ArgumentException>().WithParameterName("sdp").WithMessage("*non-empty SDP offer*");
    }

    [Fact]
    public void RealtimeTransports_EmitOnlyTheFieldsRequiredByTheirTransport()
    {
        var rtc = ThreadRealtimeTransport.WebRtc("v=0\r\no=offer");
        rtc.Type.Should().Be("webrtc"); rtc.Sdp.Should().Be("v=0\r\no=offer");
        JsonElement.DeepEquals(rtc.ToJson(), JsonSerializer.SerializeToElement(new { type = "webrtc", sdp = "v=0\r\no=offer" })).Should().BeTrue();
        var socket = ThreadRealtimeTransport.WebSocket;
        socket.Type.Should().Be("websocket"); socket.Sdp.Should().BeNull(); socket.ToJson().GetRawText().Should().Be("{\"type\":\"websocket\"}");
    }

    [Theory]
    [InlineData("\"string-id\"", "string-id")]
    [InlineData("\"\"", "")]
    [InlineData("9223372036854775807", "9223372036854775807")]
    [InlineData("-7", "-7")]
    public void RequestIds_CloneTheirPayloadAndUseInvariantText(string json, string expected)
    {
        CodexRequestId? id;
        using (var document = JsonDocument.Parse(json)) CodexRequestId.TryParse(document.RootElement, out id).Should().BeTrue();
        id!.ValueText.Should().Be(expected); id.ToString().Should().Be(expected); id.Raw.GetRawText().Should().Be(json);
        if (json.StartsWith('"')) { id.StringValue.Should().Be(expected); id.IntegerValue.Should().BeNull(); }
        else { id.IntegerValue.Should().Be(long.Parse(expected)); id.StringValue.Should().BeNull(); }
    }

    [Fact]
    public void KnownAccountTypes_ExposeTheirProtocolDiscriminators()
    {
        var raw = Json("{\"future\":true}");
        CodexAccountInfo key = new CodexApiKeyAccountInfo(raw); CodexAccountInfo chat = new CodexChatGptAccountInfo("mail@example.test", CodexPlanType.Plus, raw);
        key.Type.Should().Be("apiKey"); chat.Type.Should().Be("chatgpt"); JsonElement.DeepEquals(key.Raw, raw).Should().BeTrue(); JsonElement.DeepEquals(chat.Raw, raw).Should().BeTrue();
    }
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("codex", null)]
    [InlineData("codex/", null)]
    [InlineData("codex/future", null)]
    [InlineData("codex/1.2.3", "1.2.3")]
    [InlineData("codex/1.2.3-alpha.1 (Linux)", "1.2.3")]
    [InlineData("codex/1.2.3.4 extra/9.9", "1.2.3.4")]
    public void InitializeResult_ParsesBestEffortVersionWithoutDiscardingUserAgent(string? agent, string? expected)
    {
        var raw = JsonSerializer.SerializeToElement(new { userAgent = agent, future = 42 });
        var result = new AppServerInitializeResult(raw);
        result.UserAgent.Should().Be(agent); result.CodexBuildVersion?.ToString().Should().Be(expected);
        result.Raw.GetProperty("future").GetInt32().Should().Be(42);
        foreach (var malformed in new[] { "null", "[]", "{}", "{\"userAgent\":42}" })
        {
            var unknown = new AppServerInitializeResult(Json(malformed));
            unknown.UserAgent.Should().BeNull(); unknown.CodexBuildVersion.Should().BeNull();
        }
    }

    [Fact]
    public void NotificationAndClientIdentityConstructors_RejectMissingIdentity()
    {
        var invalid = new (Action Construct, string Parameter)[]
        {
            (() => new AppServerClientInfo(null!, "Title", "1"), "name"),
            (() => new AppServerClientInfo("name", null!, "1"), "title"),
            (() => new AppServerClientInfo("name", "Title", null!), "version"),
            (() => new AppServerRpcNotification(null!, Json("{}")), "method"),
            (() => new JKToolKit.CodexSDK.AppServer.Notifications.UnknownNotification(null!, Json("{}")), "method")
        };
        foreach (var (construct, parameter) in invalid)
            construct.Should().Throw<ArgumentNullException>().WithParameterName(parameter);
    }

    [Fact]
    public void InputFactories_KeepMentionSkillAndImagePayloadsDistinct()
    {
        var cases = new (TurnInputItem Item, string Expected)[]
        {
            (TurnInputItem.Mention("Name", "app://connector"), """{"type":"mention","name":"Name","path":"app://connector"}"""),
            (TurnInputItem.Skill("Name", "/skills/demo"), """{"type":"skill","name":"Name","path":"/skills/demo"}"""),
            (TurnInputItem.LocalImage("/images/demo.png"), """{"type":"localImage","path":"/images/demo.png"}"""),
            (TurnInputItem.ImageUrl("https://images.test/demo"), """{"type":"image","url":"https://images.test/demo"}""")
        };
        foreach (var (item, expected) in cases)
            JsonElement.DeepEquals(JsonSerializer.SerializeToElement(item.Wire, CodexAppServerClient.CreateDefaultSerializerOptions()), Json(expected)).Should().BeTrue();
        Action absent = () => new TurnInputItem(null!); absent.Should().Throw<ArgumentNullException>().WithParameterName("wire");
        foreach (var invalid in new string?[] { null, "", " " })
        {
            Action url = () => TurnInputItem.ImageUrl(invalid!); Action file = () => TurnInputItem.ImageFile(invalid!);
            url.Should().Throw<ArgumentException>().WithParameterName("url"); file.Should().Throw<ArgumentException>().WithParameterName("fileId");
        }
    }

}
