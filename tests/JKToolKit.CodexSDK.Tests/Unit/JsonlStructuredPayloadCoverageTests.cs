using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using static JKToolKit.CodexSDK.Tests.Unit.JsonlParserCoverageTests;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlStructuredPayloadCoverageTests
{
    private static T Event<T>(string body) where T : CodexEvent => Parse("event_msg", body).Should().BeOfType<T>().Subject;

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", null)]
    [InlineData("\"true\"", null)]
    public void PatchChanges_PreserveOperationsAndBooleanShape(string flag, bool? expected)
    {
        const string changes = """{"ADD":{"add":{"content":"new"}},"empty":{"add":{}},"update":{"update":{"unified_diff":"diff","move_path":"next","original_content":"before","new_content":"after"}},"delete":{"delete":{}},"bad":false,"unknown":{},"wrong":{"add":false,"update":null,"delete":[]}}""";
        var begin = Event<PatchApplyBeginEvent>("{\"type\":\"patch_apply_begin\",\"call_id\":\"c\",\"auto_approved\":" + flag + ",\"changes\":" + changes + "}");
        begin.AutoApproved.Should().Be(expected);
        begin.Changes.Should().HaveCount(4);
        begin.Changes["add"].Add!.Content.Should().Be("new");
        begin.Changes["empty"].Add!.Content.Should().BeEmpty();
        begin.Changes["update"].Update.Should().Be(new PatchApplyUpdateOperation("diff", "next", "before", "after"));
        begin.Changes["delete"].Delete.Should().NotBeNull();
        var end = Event<PatchApplyEndEvent>("{\"type\":\"patch_apply_end\",\"call_id\":\"c\",\"success\":" + flag + ",\"stdout\":\"out\",\"stderr\":\"err\",\"status\":\"done\",\"changes\":" + changes + "}");
        end.Success.Should().Be(expected);
        end.Stdout.Should().Be("out"); end.Stderr.Should().Be("err"); end.Status.Should().Be("done");
        end.Changes.Should().BeEquivalentTo(begin.Changes);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void PatchChanges_InvalidOrEmpty_IsEmptyForBeginAndNullForEnd(string changes)
    {
        Event<PatchApplyBeginEvent>("{\"type\":\"patch_apply_begin\",\"call_id\":\"c\",\"changes\":" + changes + "}").Changes.Should().BeEmpty();
        Event<PatchApplyEndEvent>("{\"type\":\"patch_apply_end\",\"call_id\":\"c\",\"changes\":" + changes + "}").Changes.Should().BeNull();
    }

    [Fact]
    public void ReviewAndPlan_SkipNonObjectsAndPreservePartialObjects()
    {
        var entered = Event<EnteredReviewModeEvent>("""{"type":"entered_review_mode","prompt":"p","user_facing_hint":"h","target":{"branch":"b","sha":"s","title":"t","instructions":"i"}}""");
        entered.UserFacingHint.Should().Be("h");
        entered.Target.Should().Be(new ReviewTarget("unknown", "b", "s", "t", "i"));
        Event<EnteredReviewModeEvent>("""{"type":"entered_review_mode","target":false}""").Target.Should().BeNull();
        var plan = Event<PlanUpdateEvent>("""{"type":"plan_update","plan":[null,{}, {"step":"step","status":"completed"}]}""");
        plan.Plan.Should().Equal(new PlanStep("", ""), new PlanStep("step", "completed"));
        var review = Event<ExitedReviewModeEvent>("""{"type":"exited_review_mode","review_output":{"overall_correctness":"correct","overall_explanation":"explanation","overall_confidence_score":"0.75","findings":[null,{}, {"priority":"2","confidence_score":"bad","title":"title","body":"body","codeLocation":{"absolute_file_path":"/a","line_range":{"start":"3","end":4}}}, {"code_location":false},{"code_location":{}},{"code_location":{"line_range":false}}]}}""").ReviewOutput!;
        review.OverallConfidenceScore.Should().Be(0.75);
        review.Findings.Should().HaveCount(5);
        review.Findings[1].Priority.Should().Be(2);
        review.Findings[1].ConfidenceScore.Should().BeNull();
        review.Findings[1].CodeLocation.Should().Be(new ReviewCodeLocation("/a", new ReviewLineRange(3, 4)));
        review.Findings.Where((_, i) => i != 1).Should().OnlyContain(f => f.CodeLocation == null);
        Event<ExitedReviewModeEvent>("""{"type":"exited_review_mode","review_output":false}""").ReviewOutput.Should().BeNull();
    }

    [Fact]
    public void TokenCount_NewUsageAndLegacyPrecedenceAndRateLimitMetadata()
    {
        var evt = Event<TokenCountEvent>("""{"type":"token_count","input_tokens":99,"info":{"last_token_usage":{"input_tokens":"1","cached_input_tokens":2,"output_tokens":3,"reasoning_output_tokens":4,"total_tokens":10},"total_token_usage":{"total_tokens":100},"model_context_window":200},"rate_limits":{"primary":{"used_percent":12.5,"window_minutes":30,"resets_at":"1000"},"secondary":{"used_percent":50,"window_minutes":60,"resets_at":2000},"credits":{"has_credits":true,"unlimited":false,"balance":"3.5"}}}""");
        evt.InputTokens.Should().Be(99); evt.OutputTokens.Should().Be(3); evt.ReasoningTokens.Should().Be(4);
        evt.LastTokenUsage.Should().Be(new TokenUsage(1, 2, 3, 4, 10));
        evt.TotalTokenUsage!.TotalTokens.Should().Be(100); evt.ModelContextWindow.Should().Be(200);
        evt.RateLimits!.Primary.Should().Be(new RateLimitScope(12.5, 30, DateTimeOffset.FromUnixTimeSeconds(1000)));
        evt.RateLimits.Secondary.Should().Be(new RateLimitScope(50, 60, DateTimeOffset.FromUnixTimeSeconds(2000)));
        evt.RateLimits.Credits.Should().Be(new RateLimitCredits(true, false, "3.5"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"primary\":false,\"secondary\":[],\"credits\":0}")]
    public void RateLimits_InvalidScopesYieldNoLimits(string limits)
    {
        Event<TokenCountEvent>("{\"type\":\"token_count\",\"rate_limits\":" + limits + "}").RateLimits.Should().BeNull();
    }

    [Fact]
    public void RateLimits_OptionalFieldsAndRelativeReset()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = Event<TokenCountEvent>("""{"type":"token_count","info":{"last_token_usage":null,"total_token_usage":false,"model_context_window":"bad"},"input_tokens":"bad","output_tokens":false,"reasoning_output_tokens":null,"rate_limits":{"primary":{"used_percent":"bad","window_minutes":false,"resets_in_seconds":30},"secondary":{"resets_at":"bad"},"credits":{"has_credits":false,"unlimited":true,"balance":null}}}""");
        evt.InputTokens.Should().BeNull(); evt.LastTokenUsage.Should().BeNull();
        evt.RateLimits!.Primary!.ResetsAt.Should().BeOnOrAfter(before.AddSeconds(30)).And.BeOnOrBefore(DateTimeOffset.UtcNow.AddSeconds(30));
        evt.RateLimits.Primary.UsedPercent.Should().BeNull(); evt.RateLimits.Primary.WindowMinutes.Should().BeNull();
        evt.RateLimits.Secondary.Should().Be(new RateLimitScope(null, null, null));
        evt.RateLimits.Credits.Should().Be(new RateLimitCredits(false, true, null));
        Event<TokenCountEvent>("""{"type":"token_count","rate_limits":{"primary":{},"secondary":{"resets_at":false},"credits":{"has_credits":"yes","unlimited":0}}}""").RateLimits!.Credits.Should().Be(new RateLimitCredits(null, null, null));
    }

    [Fact]
    public void ToolEvents_FilterInvalidArrayEntriesAndPreserveStructuredValues()
    {
        var web = Event<WebSearchEndEvent>("""{"type":"web_search_end","call_id":"c","query":"q","action":{"type":"search","query":"one","queries":["two",false,"",null," "],"url":"https://example.test","pattern":"find"}}""");
        web.Query.Should().Be("q"); web.Action!.Queries.Should().Equal("two"); web.Action.Url.Should().Be("https://example.test"); web.Action.Pattern.Should().Be("find");
        var exec = Event<ExecCommandEndEvent>("""{"type":"exec_command_end","call_id":"c","command":["echo",false," ","ok"],"exit_code":"4"}""");
        exec.Command.Should().Equal("echo", "ok"); exec.ExitCode.Should().Be(4);
        var mcp = Event<McpToolCallEndEvent>("""{"type":"mcp_tool_call_end","call_id":"c","invocation":{"server":"s","tool":"t","arguments":"args"},"result":"result"}""");
        mcp.Server.Should().Be("s"); mcp.Tool.Should().Be("t"); mcp.ArgumentsJson.Should().Be("args"); mcp.ResultJson.Should().Be("result");
        Event<McpToolCallEndEvent>("""{"type":"mcp_tool_call_end","call_id":"c","invocation":{"arguments":null}}""").ArgumentsJson.Should().BeNull();
        Event<TurnItemCompletedEvent>("""{"type":"item_completed","item":{"text":" ","content":[false,{}, {"text":""},{"text":"first"},{"text":"second"}]}}""").Text.Should().Be("first");
    }
}
