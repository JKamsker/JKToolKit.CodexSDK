using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerNotificationValueContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("1.25e2", "1.25e2")]
    [InlineData("true", "True")]
    [InlineData("false", "False")]
    [InlineData("null", null)]
    [InlineData("{}", null)]
    [InlineData("[]", null)]
    public void ScalarText_OnlyConvertsScalarKindsAndPreservesNumberSpelling(string value, string? expected)
    {
        AppServerNotificationParsing.GetScalarText(Json(value)).Should().Be(expected);
        AppServerNotificationParsing.GetScalarText(Json("{\"v\":" + value + "}"), "v").Should().Be(expected);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public void MissingProperty_UsesSafeDefaults(string json)
    {
        AppServerNotificationParsing.GetScalarText(Json(json), "v").Should().BeNull();
        AppServerNotificationParsing.GetAny(Json(json), "v").GetRawText().Should().Be("{}");
        AppServerNotificationParsing.ParseFuzzyFileSearchResults(Json(json)).Should().BeEmpty();
    }

    [Fact]
    public void GetAny_ClonesSelectedPayload()
    {
        JsonElement result;
        using (var document = JsonDocument.Parse("{\"v\":{\"future\":true}}"))
            result = AppServerNotificationParsing.GetAny(document.RootElement, "v");
        result.GetProperty("future").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(null, FuzzyFileSearchMatchType.Unknown)]
    [InlineData(" ", FuzzyFileSearchMatchType.Unknown)]
    [InlineData("future", FuzzyFileSearchMatchType.Unknown)]
    [InlineData(" FILE ", FuzzyFileSearchMatchType.File)]
    [InlineData("path", FuzzyFileSearchMatchType.File)]
    [InlineData("filename", FuzzyFileSearchMatchType.File)]
    [InlineData("file_name", FuzzyFileSearchMatchType.File)]
    [InlineData("directory", FuzzyFileSearchMatchType.Directory)]
    [InlineData("dir", FuzzyFileSearchMatchType.Directory)]
    public void FuzzySearch_NormalizesMatchKindWithoutChangingWireValue(string? matchType, FuzzyFileSearchMatchType expected)
    {
        var result = AppServerNotificationParsing.ParseFuzzyFileSearchResults(JsonSerializer.SerializeToElement(new
        {
            files = new[] { new { root = "/repo", path = "src/a.cs", file_name = "a.cs", match_type = matchType, score = "4294967295", indices = new object?[] { 0, 3, -1, 1.5, "2", null, 4294967296L } } }
        })).Should().ContainSingle().Subject;
        result.Root.Should().Be("/repo"); result.Path.Should().Be("src/a.cs"); result.FileName.Should().Be("a.cs");
        result.MatchType.Should().Be(matchType); result.MatchKind.Should().Be(expected);
        result.Score.Should().Be(uint.MaxValue); result.Indices.Should().Equal(0u, 3u);
    }

    [Theory]
    [InlineData("null", 0u)]
    [InlineData("true", 0u)]
    [InlineData("-1", 0u)]
    [InlineData("4294967296", 0u)]
    [InlineData("\"invalid\"", 0u)]
    [InlineData("17", 17u)]
    [InlineData("\"18\"", 18u)]
    public void FuzzySearch_ValidatesScoreAndIgnoresNonObjectEntries(string score, uint expected)
    {
        var item = AppServerNotificationParsing.ParseFuzzyFileSearchResults(Json("{\"files\":[null,42,{\"score\":" + score + ",\"indices\":false}]}"))
            .Should().ContainSingle().Subject;
        item.Root.Should().BeEmpty(); item.Path.Should().BeEmpty(); item.FileName.Should().BeEmpty();
        item.Score.Should().Be(expected); item.Indices.Should().BeNull();
    }

    [Fact]
    public void FuzzySearch_MissingScoreDefaultsToZeroAndEmptyIndicesRemainEmpty()
    {
        var item = AppServerNotificationParsing.ParseFuzzyFileSearchResults(Json("{\"files\":[{\"indices\":[]}]}"))[0];
        item.Score.Should().Be(0); item.Indices.Should().BeEmpty();
    }

    [Theory]
    [InlineData("starting", McpServerStartupState.Starting)]
    [InlineData("ready", McpServerStartupState.Ready)]
    [InlineData("failed", McpServerStartupState.Failed)]
    [InlineData("cancelled", McpServerStartupState.Cancelled)]
    public void McpStartup_MapsKnownStates(string wire, McpServerStartupState expected)
    {
        var notification = AppServerNotificationMapper.Map("mcpServer/startupStatus/updated", JsonSerializer.SerializeToElement(new { name = "server", status = wire, error = "detail", threadId = "thread" }))
            .Should().BeOfType<McpServerStartupStatusUpdatedNotification>().Subject;
        notification.Name.Should().Be("server"); notification.Status.Should().Be(expected); notification.Error.Should().Be("detail"); notification.ThreadId.Should().Be("thread");
    }

    [Theory]
    [InlineData("inProgress", GuardianApprovalReviewStatus.InProgress, "low", GuardianRiskLevel.Low, "unknown", GuardianUserAuthorization.Unknown)]
    [InlineData(" approved ", GuardianApprovalReviewStatus.Approved, " medium ", GuardianRiskLevel.Medium, " low ", GuardianUserAuthorization.Low)]
    [InlineData("denied", GuardianApprovalReviewStatus.Denied, "high", GuardianRiskLevel.High, "medium", GuardianUserAuthorization.Medium)]
    [InlineData("aborted", GuardianApprovalReviewStatus.Aborted, "critical", GuardianRiskLevel.Critical, "high", GuardianUserAuthorization.High)]
    [InlineData("future", GuardianApprovalReviewStatus.Unknown, "future", GuardianRiskLevel.Unknown, "future", GuardianUserAuthorization.Unknown)]
    [InlineData("approved", GuardianApprovalReviewStatus.Approved, null, null, null, null)]
    public void GuardianReview_MapsEnumsAndPreservesOriginalStatus(string wire, GuardianApprovalReviewStatus status, string? risk, GuardianRiskLevel? level, string? authorization, GuardianUserAuthorization? userAuthorization)
    {
        var json = JsonSerializer.SerializeToElement(new { review = new { status = wire, rationale = "explanation", riskScore = 3, riskLevel = risk, userAuthorization = authorization } });
        AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(json, "review", out var review).Should().BeTrue();
        review.Status.Should().Be(status); review.StatusValue.Should().Be(wire); review.Rationale.Should().Be("explanation");
        review.RiskScore.Should().Be(3); review.RiskLevel.Should().Be(level); review.UserAuthorization.Should().Be(userAuthorization);
        review.Raw.GetProperty("status").GetString().Should().Be(wire);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"review\":null}")]
    [InlineData("{\"review\":{}}")]
    [InlineData("{\"review\":{\"status\":\" \"}}")]
    [InlineData("{\"review\":{\"status\":\"approved\",\"rationale\":false}}")]
    [InlineData("{\"review\":{\"status\":\"approved\",\"riskScore\":2147483648}}")]
    [InlineData("{\"review\":{\"status\":\"approved\",\"riskLevel\":false}}")]
    [InlineData("{\"review\":{\"status\":\"approved\",\"userAuthorization\":false}}")]
    public void GuardianReview_RejectsMalformedFields(string json) =>
        AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(Json(json), "review", out _).Should().BeFalse();

    [Theory]
    [InlineData("null", null, 0, 0, null)]
    [InlineData("{}", null, 0, 0, null)]
    [InlineData("{\"itemId\":42,\"data\":false,\"sampleRate\":true,\"numChannels\":null,\"samplesPerChannel\":null}", null, 0, 0, null)]
    [InlineData("{\"data\":\"AA==\",\"sampleRate\":24000,\"numChannels\":2,\"samplesPerChannel\":64}", "AA==", 24000, 2, 64)]
    [InlineData("{\"data\":\"BB==\",\"sampleRate\":\"48000\",\"numChannels\":\"1\",\"samplesPerChannel\":\"128\"}", "BB==", 48000, 1, 128)]
    [InlineData("{\"sampleRate\":2147483648,\"numChannels\":\"bad\",\"samplesPerChannel\":2147483648}", null, 0, 0, null)]
    [InlineData("{\"sampleRate\":1.5,\"numChannels\":{},\"samplesPerChannel\":\"bad\"}", null, 0, 0, null)]
    [InlineData("{\"samplesPerChannel\":true}", null, 0, 0, null)]
    public void AudioNotification_ExposesTolerantConvenienceFields(string audio, string? data, int rate, int channels, int? samples)
    {
        var item = new ThreadRealtimeOutputAudioDeltaNotification("thread", Json(audio), Json("{}"));
        item.ThreadId.Should().Be("thread"); item.Method.Should().Be("thread/realtime/outputAudio/delta");
        item.Audio.GetRawText().Should().Be(audio); item.Data.Should().Be(data);
        item.SampleRate.Should().Be(rate); item.NumChannels.Should().Be(channels); item.SamplesPerChannel.Should().Be(samples);
        item.ItemId.Should().BeNull();
    }

    [Fact]
    public void AudioNotification_ExposesItemId() =>
        new ThreadRealtimeOutputAudioDeltaNotification("thread", Json("{\"itemId\":\"item\"}"), Json("{}")).ItemId.Should().Be("item");

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"type\":42,\"activeFlags\":null}")]
    [InlineData("{\"activeFlags\":{}}")]
    public void StatusNotification_MalformedFieldsAreUnknown(string status)
    {
        var item = new ThreadStatusChangedNotification("thread", Json(status), Json("{}"));
        item.ThreadId.Should().Be("thread"); item.Method.Should().Be("thread/status/changed");
        item.Status.GetRawText().Should().Be(status); item.StatusType.Should().BeNull(); item.ActiveFlags.Should().BeNull();
    }

    [Fact]
    public void StatusNotification_FiltersNonStringFlags()
    {
        var item = new ThreadStatusChangedNotification("t", Json("""{"type":"active","activeFlags":["waitingOnApproval",42,null,"waitingOnUserInput"]}"""), Json("{}"));
        item.StatusType.Should().Be("active"); item.ActiveFlags.Should().Equal("waitingOnApproval", "waitingOnUserInput");
    }    [Theory]
    [InlineData("{}")]
    [InlineData("{\"riskScore\":null}")]
    public void GuardianReview_AbsentOrNullRiskScoreRemainsUnknown(string fields)
    {
        var reviewJson = System.Text.Json.Nodes.JsonNode.Parse(fields)!.AsObject();
        reviewJson["status"] = "approved";
        var payload = Json(new System.Text.Json.Nodes.JsonObject { ["review"] = reviewJson }.ToJsonString());
        AppServerNotificationParsing.TryParseGuardianApprovalReviewInfo(payload, "review", out var review).Should().BeTrue();
        review.RiskScore.Should().BeNull(); review.Status.Should().Be(GuardianApprovalReviewStatus.Approved);
    }


}
