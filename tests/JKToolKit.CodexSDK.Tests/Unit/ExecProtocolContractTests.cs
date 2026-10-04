using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecProtocolContractTests
{
    private readonly JsonlEventParser _parser = new(NullLogger<JsonlEventParser>.Instance);

    [Fact]
    public void PatchOperations_PreserveContentAndRenameMetadata()
    {
        var evt = Parse<PatchApplyBeginEvent>("""{"type":"event_msg","payload":{"type":"patch_apply_begin","call_id":"patch","auto_approved":false,"changes":{"added":{"add":{"content":"new"}},"updated":{"update":{"unified_diff":"diff","move_path":"renamed","original_content":"old","new_content":"changed"}},"deleted":{"delete":{}}}}}""");
        Assert.Equal("new", evt.Changes["added"].Add!.Content);
        var update = evt.Changes["updated"].Update!;
        Assert.Equal("diff", update.UnifiedDiff);
        Assert.Equal("renamed", update.MovePath);
        Assert.Equal("old", update.OriginalContent);
        Assert.Equal("changed", update.NewContent);
        Assert.NotNull(evt.Changes["deleted"].Delete);
        Assert.Null(evt.Changes["deleted"].Add);
    }

    [Fact]
    public void Review_ExposesTargetAndAllFindingMetadata()
    {
        var start = Parse<EnteredReviewModeEvent>("""{"type":"event_msg","payload":{"type":"entered_review_mode","target":{"type":"commit","branch":"main","sha":"abc","title":"subject","instructions":"review"}}}""");
        Assert.Equal(new ReviewTarget("commit", "main", "abc", "subject", "review"), start.Target);
        Assert.Equal("commit", start.Target!.Type);
        Assert.Equal("main", start.Target.Branch);
        Assert.Equal("abc", start.Target.Sha);
        Assert.Equal("subject", start.Target.Title);
        Assert.Equal("review", start.Target.Instructions);
        var end = Parse<ExitedReviewModeEvent>("""{"type":"event_msg","payload":{"type":"exited_review_mode","review_output":{"overall_correctness":"incorrect","overall_explanation":"regression","overall_confidence_score":0.9,"findings":[{"priority":1,"confidence_score":0.8,"title":"bug","body":"details","code_location":{"absolute_file_path":"/repo/file","line_range":{"start":2,"end":3}}}]}}}""");
        var review = end.ReviewOutput!;
        Assert.Equal("incorrect", review.OverallCorrectness);
        Assert.Equal("regression", review.OverallExplanation);
        Assert.Equal(0.9, review.OverallConfidenceScore);
        var finding = Assert.Single(review.Findings);
        Assert.Equal(1, finding.Priority);
        Assert.Equal(0.8, finding.ConfidenceScore);
        Assert.Equal("bug", finding.Title);
        Assert.Equal("details", finding.Body);
        Assert.Equal("/repo/file", finding.CodeLocation!.AbsoluteFilePath);
        Assert.Equal(2, finding.CodeLocation.LineRange!.Start);
        Assert.Equal(3, finding.CodeLocation.LineRange.End);
    }

    [Fact]
    public void Usage_ExposesAllCountersAndRateLimitScopes()
    {
        var evt = Parse<TokenCountEvent>("""{"type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":11,"cached_input_tokens":3,"output_tokens":7,"reasoning_output_tokens":2,"total_tokens":18}},"rate_limits":{"primary":{"used_percent":12.5,"window_minutes":300,"resets_at":1700000000},"secondary":{"used_percent":4,"window_minutes":10080,"resets_at":"1700000001"},"credits":{"has_credits":true,"unlimited":false,"balance":"25.50"}}}}""");
        var usage = evt.LastTokenUsage!;
        Assert.Equal(11, usage.InputTokens);
        Assert.Equal(3, usage.CachedInputTokens);
        Assert.Equal(7, usage.OutputTokens);
        Assert.Equal(2, usage.ReasoningOutputTokens);
        Assert.Equal(18, usage.TotalTokens);
        var limits = evt.RateLimits!;
        Assert.Equal(12.5, limits.Primary!.UsedPercent);
        Assert.Equal(300, limits.Primary.WindowMinutes);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), limits.Primary.ResetsAt);
        Assert.Equal(4, limits.Secondary!.UsedPercent);
        Assert.Equal(10080, limits.Secondary.WindowMinutes);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000001), limits.Secondary.ResetsAt);
        Assert.True(limits.Credits!.HasCredits);
        Assert.False(limits.Credits.Unlimited);
        Assert.Equal("25.50", limits.Credits.Balance);
    }

    [Fact]
    public void GhostSnapshot_PreservesUntrackedPaths()
    {
        var evt = Parse<ResponseItemEvent>("""{"type":"response_item","payload":{"type":"ghost_snapshot","ghost_commit":{"id":"a","parent":"b","preexisting_untracked_files":["file"],"preexisting_untracked_dirs":["dir"]}}}""");
        var ghost = Assert.IsType<GhostSnapshotResponseItemPayload>(evt.Payload).GhostCommit!;
        Assert.Equal("a", ghost.Id);
        Assert.Equal("b", ghost.Parent);
        Assert.Equal(new[] { "file" }, ghost.PreexistingUntrackedFiles);
        Assert.Equal(new[] { "dir" }, ghost.PreexistingUntrackedDirs);
    }

    [Fact]
    public void UnknownContentParts_RetainRawFieldsForForwardCompatibility()
    {
        var message = Assert.IsType<MessageResponseItemPayload>(Parse<ResponseItemEvent>("""{"type":"response_item","payload":{"type":"message","role":"assistant","content":[{"type":"input_image","image_url":"image"},{"type":"future","value":42}]}}""").Payload);
        Assert.Equal("image", Assert.IsType<ResponseMessageInputImagePart>(message.Content![0]).ImageUrl);
        Assert.Equal(42, Assert.IsType<UnknownResponseMessageContentPart>(message.Content[1]).Raw.GetProperty("value").GetInt32());
        var reasoning = Assert.IsType<ReasoningResponseItemPayload>(Parse<ResponseItemEvent>("""{"type":"response_item","payload":{"type":"reasoning","content":[{"type":"future","value":43}]}}""").Payload);
        Assert.Equal(43, Assert.IsType<UnknownReasoningContentPart>(Assert.Single(reasoning.Content!)).Raw.GetProperty("value").GetInt32());
        var tool = Assert.IsType<FunctionCallOutputResponseItemPayload>(Parse<ResponseItemEvent>("""{"type":"response_item","payload":{"type":"function_call_output","call_id":"call","output":[{"type":"future","value":44}]}}""").Payload);
        Assert.Equal(44, Assert.IsType<UnknownFunctionToolOutputContentPart>(Assert.Single(tool.OutputContent!)).Raw.GetProperty("value").GetInt32());
    }

    private T Parse<T>(string line) where T : CodexEvent
    {
        Assert.True(_parser.TryParseLine("{\"timestamp\":\"2026-10-03T00:00:00Z\"," + line[1..], out var evt, out var error), error);
        return Assert.IsType<T>(evt);
    }
}
