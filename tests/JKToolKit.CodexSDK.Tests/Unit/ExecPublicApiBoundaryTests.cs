using System.Text.Json;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecPublicApiBoundaryTests
{
    [Theory]
    [InlineData("agent", "hello")]
    [InlineData("reasoning", "hello")]
    [InlineData("user", "hello")]
    [InlineData("agent", null)]
    [InlineData("reasoning", " ")]
    [InlineData("user", "")]
    public void TextCandidates_PreserveTextAndSkipEmptyMessages(string kind, string? text)
    {
        var raw = JsonSerializer.SerializeToElement(new { });
        CodexEvent evt = kind switch
        {
            "agent" => new AgentMessageEvent { Text = text!, Timestamp = DateTimeOffset.UnixEpoch, Type = kind, RawPayload = raw },
            "reasoning" => new AgentReasoningEvent { Text = text!, Timestamp = DateTimeOffset.UnixEpoch, Type = kind, RawPayload = raw },
            _ => new UserMessageEvent { Text = text!, Timestamp = DateTimeOffset.UnixEpoch, Type = kind, RawPayload = raw }
        };
        Assert.Equal(string.IsNullOrWhiteSpace(text) ? [] : new[] { text }, evt.EnumerateTextCandidates());
    }

    [Theory]
    [InlineData("assistant", true)]
    [InlineData("AsSiStAnT", true)]
    [InlineData("user", false)]
    [InlineData(null, false)]
    public void TextCandidates_JoinOnlyAssistantMessageParts(string? role, bool expected)
    {
        var payload = new MessageResponseItemPayload
        {
            PayloadType = "message", Role = role,
            Content = [new ResponseMessageOutputTextPart { ContentType = "output_text", Text = "first" },
                new ResponseMessageOutputTextPart { ContentType = "output_text", Text = " " },
                new ResponseMessageOutputTextPart { ContentType = "output_text", Text = "second" }]
        };
        Assert.Equal(expected ? new[] { "first\nsecond" } : [], Item(payload).EnumerateTextCandidates());
        Assert.Empty(Item(payload with { Content = [] }).EnumerateTextCandidates());
    }

    [Fact]
    public void TextCandidates_KeepNonblankReasoningSummariesInOrder_AndIgnoreOtherEvents()
    {
        var payload = new ReasoningResponseItemPayload { PayloadType = "reasoning", Content = [], SummaryTexts = ["first", "", " ", null!, "second"] };
        Assert.Equal(["first", "second"], Item(payload).EnumerateTextCandidates());
        Assert.Empty(Item(payload with { SummaryTexts = [] }).EnumerateTextCandidates());
        Assert.Empty(Item(payload with { SummaryTexts = null! }).EnumerateTextCandidates());
        Assert.Empty(ExecEventPipelineBehaviorTests.Event().EnumerateTextCandidates());
        Assert.Equal("evt", Assert.Throws<ArgumentNullException>(() => ((CodexEvent)null!).EnumerateTextCandidates().ToArray()).ParamName);
    }

    [Fact]
    public void SessionInfo_DisplayIncludesOnlyAvailableMetadata_AndCopyPreservesOriginal()
    {
        var original = new CodexSessionInfo(SessionId.Parse("session"), "original.jsonl", DateTimeOffset.UnixEpoch);
        Assert.Equal("Session session (Created: 1970-01-01 00:00:00)", original.ToString());
        var updated = original with { LogPath = "new.jsonl", Model = CodexModel.Parse("custom"), ModelProvider = "provider", HumanLabel = "label", UpdatedAt = DateTimeOffset.UnixEpoch.AddHours(1) };
        Assert.Equal("Session session (Created: 1970-01-01 00:00:00, Updated: 1970-01-01 01:00:00, Model: custom, Provider: provider, Label: label)", updated.ToString());
        Assert.Equal(original.ToString(), (original with { ModelProvider = " ", HumanLabel = "" }).ToString());
        Assert.Equal("original.jsonl", original.LogPath);
        Assert.Equal("new.jsonl", updated.LogPath);
        Assert.Throws<ArgumentNullException>(() => original with { LogPath = null! });
        Assert.Throws<ArgumentException>(() => original with { LogPath = " " });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SessionInfo_ConstructorRejectsInvalidLogPath(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CodexSessionInfo(SessionId.Parse("session"), path!, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task DefaultClient_CanBeCreatedDisposedAndRejectsNullInputsWithoutLaunching()
    {
        await using var client = new CodexClient();
        Assert.Throws<ArgumentNullException>(() => new CodexClient((CodexClientOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new CodexClient((IOptions<CodexClientOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => new CodexClient(new OptionsWrapper<CodexClientOptions>(null!)));
        Assert.Equal("target", (await Assert.ThrowsAsync<ArgumentNullException>(() => client.ResumeSessionAsync((CodexResumeTarget)null!))).ParamName);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ResumeSessionAsync(SessionId.Parse("session"), new CodexSessionOptions(Path.GetTempPath(), "prompt"), canceled.Token));
        client.Dispose();
        await client.DisposeAsync();
    }

    private static ResponseItemEvent Item(ResponseItemPayload payload) => new()
    {
        Payload = payload, PayloadType = payload.PayloadType, Type = "response_item",
        Timestamp = DateTimeOffset.UnixEpoch, RawPayload = JsonSerializer.SerializeToElement(new { })
    };
}
