using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using static JKToolKit.CodexSDK.Tests.Unit.JsonlParserCoverageTests;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlCollabCoverageTests
{
    [Theory]
    [InlineData("pending_init", CollabReceiverStatus.PendingInit)]
    [InlineData("running", CollabReceiverStatus.Running)]
    [InlineData("interrupted", CollabReceiverStatus.Interrupted)]
    [InlineData("completed", CollabReceiverStatus.Completed)]
    [InlineData("errored", CollabReceiverStatus.Errored)]
    [InlineData("shutdown", CollabReceiverStatus.Shutdown)]
    [InlineData("not_found", CollabReceiverStatus.NotFound)]
    [InlineData("future", CollabReceiverStatus.Unknown)]
    public void CollabEnd_ConvertsAllStatuses(string status, CollabReceiverStatus expected)
    {
        foreach (var type in new[] { "collab_close_end", "collab_resume_end" })
        {
            var evt = Parse("event_msg", "{\"type\":\"" + type + "\",\"call_id\":\"c\",\"sender_thread_id\":\"s\",\"receiver_thread_id\":\"r\",\"status\":\"" + status + "\"}");
            var end = (CollabEndEventBase)evt;
            end.Status.Should().Be(expected); end.StatusInfo!.Payload.Should().BeNull(); end.StatusInfo.PayloadText.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("\"text\"", "text")]
    [InlineData("null", null)]
    [InlineData("42", null)]
    [InlineData("{\"text\":\"text\",\"message\":\"ignored\"}", "text")]
    [InlineData("{\"message\":\"message\"}", "message")]
    [InlineData("{\"payload\":{\"text\":\"nested\"}}", "nested")]
    [InlineData("{\"payload\":{\"message\":\"nested message\"}}", "nested message")]
    [InlineData("{\"payload\":false}", null)]
    [InlineData("{}", null)]
    public void StatusUnion_PreservesPayloadAndExtractsText(string payload, string? expected)
    {
        var evt = Parse("event_msg", "{\"type\":\"collab_close_end\",\"call_id\":\"c\",\"sender_thread_id\":\"s\",\"receiver_thread_id\":\"r\",\"status\":{\"completed\":" + payload + "}}").Should().BeOfType<CollabCloseEndEvent>().Subject;
        evt.Status.Should().Be(CollabReceiverStatus.Completed); evt.StatusInfo!.PayloadText.Should().Be(expected);
        if (payload == "null") evt.StatusInfo.Payload.Should().BeNull();
        else evt.StatusInfo.Payload!.Value.GetRawText().Should().Be(payload);
    }

    [Fact]
    public void WaitingBegin_FiltersMalformedReferencesAndUsesRoleFallback()
    {
        var evt = Parse("event_msg", """{"type":"collab_waiting_begin","call_id":"c","sender_thread_id":"s","receiver_thread_ids":[null,false," ","r"],"receiver_agents":[false,{}, {"thread_id":" "},{"thread_id":"r","agent_nickname":"nick","agent_type":"legacy"},{"thread_id":"r2","agent_role":"role","agent_type":"ignored"}]}""").Should().BeOfType<CollabWaitingBeginEvent>().Subject;
        evt.ReceiverThreadIds.Should().Equal("r");
        evt.ReceiverAgents.Should().BeEquivalentTo(new[] { new CollabAgentRef { ThreadId = "r", AgentNickname = "nick", AgentRole = "legacy" }, new CollabAgentRef { ThreadId = "r2", AgentRole = "role" } });
    }

    [Fact]
    public void WaitingEnd_FiltersInvalidStatusesAndPreservesValidMetadata()
    {
        var evt = Parse("event_msg", """{"type":"collab_waiting_end","call_id":"c","sender_thread_id":"s","statuses":{"invalid":false,"empty":{},"r":"running"},"agent_statuses":[false,{}, {"thread_id":"r"},{"thread_id":" ","status":"running"},{"thread_id":"r","agent_nickname":"nick","agent_type":"legacy","status":{"completed":"done"}}]}""").Should().BeOfType<CollabWaitingEndEvent>().Subject;
        evt.Statuses.Should().BeEquivalentTo(new Dictionary<string,string> { ["r"] = "running" });
        evt.StatusInfos.Should().ContainSingle(); evt.StatusInfos!["R"].Status.Should().Be(CollabAgentStatus.Running);
        var entry = evt.AgentStatuses.Should().ContainSingle().Subject;
        entry.ThreadId.Should().Be("r"); entry.AgentNickname.Should().Be("nick"); entry.AgentRole.Should().Be("legacy"); entry.StatusInfo.PayloadText.Should().Be("done");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("[]")]
    [InlineData("[false,{}]")]
    public void Waiting_MetadataWithoutValidEntriesIsEmpty(string metadata)
    {
        var begin = Parse("event_msg", "{\"type\":\"collab_waiting_begin\",\"call_id\":\"c\",\"sender_thread_id\":\"s\",\"receiver_thread_ids\":[],\"receiver_agents\":" + metadata + "}").Should().BeOfType<CollabWaitingBeginEvent>().Subject;
        begin.ReceiverAgents.Should().BeEmpty();
        var end = Parse("event_msg", "{\"type\":\"collab_waiting_end\",\"call_id\":\"c\",\"sender_thread_id\":\"s\",\"statuses\":{},\"agent_statuses\":" + metadata + "}").Should().BeOfType<CollabWaitingEndEvent>().Subject;
        end.Statuses.Should().BeEmpty(); end.StatusInfos.Should().BeNull(); end.AgentStatuses.Should().BeEmpty();
    }
}
