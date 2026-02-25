using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

/// <summary>
/// Unit tests for the JsonlEventParser.
/// </summary>
public class JsonlEventParserTests
{
    private readonly JsonlEventParser _parser;

    public JsonlEventParserTests()
    {
        _parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance);
    }

    [Fact]
    public async Task ParseAsync_SessionMetaEvent_ParsesCorrectly()
    {
        // Arrange
        var sessionId = SessionId.Parse("test-session-123");
        var cwd = "/home/user/project";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateSessionMeta(sessionId, cwd, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<SessionMetaEvent>().Subject;
        evt.SessionId.Should().Be(sessionId);
        evt.Cwd.Should().Be(cwd);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("session_meta");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_UserMessageEvent_ParsesCorrectly()
    {
        // Arrange
        var messageText = "Hello, Codex!";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateUserMessage(messageText, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UserMessageEvent>().Subject;
        evt.Text.Should().Be(messageText);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("user_message");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_AgentMessageEvent_ParsesCorrectly()
    {
        // Arrange
        var messageText = "I'll help you with that.";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateAgentMessage(messageText, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentMessageEvent>().Subject;
        evt.Text.Should().Be(messageText);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("agent_message");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_AgentReasoningEvent_ParsesCorrectly()
    {
        // Arrange
        var reasoningText = "Analyzing the request structure...";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateAgentReasoning(reasoningText, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentReasoningEvent>().Subject;
        evt.Text.Should().Be(reasoningText);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("agent_reasoning");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_TokenCountEvent_ParsesCorrectly()
    {
        // Arrange
        var inputTokens = 100;
        var outputTokens = 50;
        var reasoningTokens = 25;
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateTokenCount(inputTokens, outputTokens, reasoningTokens, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TokenCountEvent>().Subject;
        evt.InputTokens.Should().Be(inputTokens);
        evt.OutputTokens.Should().Be(outputTokens);
        evt.ReasoningTokens.Should().Be(reasoningTokens);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("token_count");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_TurnContextEvent_ParsesCorrectly()
    {
        // Arrange
        var approvalPolicy = "auto";
        var sandboxPolicyType = "none";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateTurnContext(approvalPolicy, sandboxPolicyType, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TurnContextEvent>().Subject;
        evt.ApprovalPolicy.Should().Be(approvalPolicy);
        evt.SandboxPolicyType.Should().Be(sandboxPolicyType);
        evt.NetworkAccess.Should().BeNull();
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("turn_context");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_TurnContextEvent_WithSandboxPolicyObject_ParsesNetworkAccess()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""turn_context"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""approval_policy"": ""never"",
                ""sandbox_policy"": {{
                    ""type"": ""workspace-write"",
                    ""network_access"": false
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TurnContextEvent>().Subject;
        evt.ApprovalPolicy.Should().Be("never");
        evt.SandboxPolicyType.Should().Be("workspace-write");
        evt.NetworkAccess.Should().BeFalse();
        evt.Type.Should().Be("turn_context");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_WrappedAgentMessage_UnwrapsToAgentMessageEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""agent_message"",
                ""message"": ""Hello from wrapper.""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentMessageEvent>().Subject;
        evt.Text.Should().Be("Hello from wrapper.");
        evt.Type.Should().Be("agent_message");

        evt.RawPayload.GetProperty("type").GetString().Should().Be("event_msg");
        evt.RawPayload.TryGetProperty("timestamp", out _).Should().BeTrue();
        var payload = evt.RawPayload.GetProperty("payload");
        payload.GetProperty("type").GetString().Should().Be("agent_message");
        payload.GetProperty("message").GetString().Should().Be("Hello from wrapper.");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_WrappedAgentMessage_WithNestedPayload_UnwrapsToAgentMessageEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""agent_message"",
                ""payload"": {{
                    ""message"": ""Hello from nested wrapper.""
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentMessageEvent>().Subject;
        evt.Text.Should().Be("Hello from nested wrapper.");
        evt.Type.Should().Be("agent_message");

        evt.RawPayload.GetProperty("type").GetString().Should().Be("event_msg");
        evt.RawPayload.TryGetProperty("timestamp", out _).Should().BeTrue();
        var payload = evt.RawPayload.GetProperty("payload");
        payload.GetProperty("type").GetString().Should().Be("agent_message");
        payload.GetProperty("payload").GetProperty("message").GetString().Should().Be("Hello from nested wrapper.");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_TaskStarted_ParsesTaskStartedEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""task_started"",
                ""turn_id"": ""turn_1"",
                ""model_context_window"": 8192
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TaskStartedEvent>().Subject;
        evt.Type.Should().Be("task_started");
        evt.TurnId.Should().Be("turn_1");
        evt.ModelContextWindow.Should().Be(8192);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_TaskComplete_ParsesTaskCompleteEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""task_complete"",
                ""turn_id"": ""turn_2"",
                ""last_agent_message"": ""done""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TaskCompleteEvent>().Subject;
        evt.Type.Should().Be("task_complete");
        evt.TurnId.Should().Be("turn_2");
        evt.LastAgentMessage.Should().Be("done");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ItemCompleted_Plan_ParsesTurnItemCompletedEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""item_completed"",
                ""thread_id"": ""thread_123"",
                ""turn_id"": ""turn_456"",
                ""item"": {{
                    ""type"": ""Plan"",
                    ""id"": ""plan_1"",
                    ""text"": ""- step 1\\n- step 2""
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TurnItemCompletedEvent>().Subject;
        evt.Type.Should().Be("item_completed");
        evt.ThreadId.Should().Be("thread_123");
        evt.TurnId.Should().Be("turn_456");
        evt.ItemType.Should().Be("Plan");
        evt.ItemId.Should().Be("plan_1");
        evt.Text.Should().Contain("step 1");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_UndoCompleted_ParsesUndoCompletedEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""undo_completed"",
                ""success"": true,
                ""message"": ""ok""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UndoCompletedEvent>().Subject;
        evt.Type.Should().Be("undo_completed");
        evt.Success.Should().BeTrue();
        evt.Message.Should().Be("ok");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ThreadRolledBack_ParsesThreadRolledBackEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""thread_rolled_back"",
                ""num_turns"": 2
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ThreadRolledBackEvent>().Subject;
        evt.Type.Should().Be("thread_rolled_back");
        evt.NumTurns.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_AgentReasoningRawContent_ParsesAgentReasoningRawContentEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""agent_reasoning_raw_content"",
                ""text"": ""raw""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentReasoningRawContentEvent>().Subject;
        evt.Type.Should().Be("agent_reasoning_raw_content");
        evt.Text.Should().Be("raw");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_WebSearchEnd_ParsesWebSearchEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""web_search_end"",
                ""call_id"": ""ws_1"",
                ""query"": ""weather: San Francisco, CA"",
                ""action"": {{ ""type"": ""open_page"", ""url"": ""https://example.com"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<WebSearchEndEvent>().Subject;
        evt.Type.Should().Be("web_search_end");
        evt.CallId.Should().Be("ws_1");
        evt.Query.Should().Contain("weather");
        evt.Action!.Type.Should().Be("open_page");
        evt.Action.Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ExecCommandEnd_ParsesExecCommandEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""exec_command_end"",
                ""call_id"": ""call_1"",
                ""turn_id"": ""turn_1"",
                ""command"": [""bash"", ""-lc"", ""echo hi""],
                ""cwd"": ""/tmp"",
                ""stdout"": ""hi\\n"",
                ""stderr"": """",
                ""aggregated_output"": ""hi\\n"",
                ""exit_code"": 0,
                ""duration"": ""1s"",
                ""formatted_output"": ""hi"",
                ""status"": ""completed"",
                ""source"": ""agent""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ExecCommandEndEvent>().Subject;
        evt.Type.Should().Be("exec_command_end");
        evt.CallId.Should().Be("call_1");
        evt.TurnId.Should().Be("turn_1");
        evt.Command.Should().ContainInOrder("bash", "-lc", "echo hi");
        evt.ExitCode.Should().Be(0);
        evt.Status.Should().Be("completed");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_McpToolCallEnd_ParsesMcpToolCallEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""mcp_tool_call_end"",
                ""call_id"": ""mcp_1"",
                ""invocation"": {{ ""server"": ""srv"", ""tool"": ""t"", ""arguments"": {{ ""a"": 1 }} }},
                ""duration"": ""1s"",
                ""result"": {{ ""Ok"": {{ ""content"": [] }} }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<McpToolCallEndEvent>().Subject;
        evt.Type.Should().Be("mcp_tool_call_end");
        evt.CallId.Should().Be("mcp_1");
        evt.Server.Should().Be("srv");
        evt.Tool.Should().Be("t");
        evt.ArgumentsJson.Should().Contain("\"a\": 1");
        evt.ResultJson.Should().Contain("Ok");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ViewImageToolCall_ParsesViewImageToolCallEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""view_image_tool_call"",
                ""call_id"": ""img_1"",
                ""path"": ""C:\\\\tmp\\\\img.png""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ViewImageToolCallEvent>().Subject;
        evt.Type.Should().Be("view_image_tool_call");
        evt.CallId.Should().Be("img_1");
        evt.Path.Should().EndWith("img.png");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabAgentSpawnEnd_ParsesCollabAgentSpawnEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_agent_spawn_end"",
                ""call_id"": ""c1"",
                ""sender_thread_id"": ""t_sender"",
                ""new_thread_id"": ""t_new"",
                ""new_agent_nickname"": ""nick"",
                ""new_agent_role"": ""worker"",
                ""prompt"": ""go"",
                ""status"": ""running""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabAgentSpawnEndEvent>().Subject;
        evt.Type.Should().Be("collab_agent_spawn_end");
        evt.CallId.Should().Be("c1");
        evt.NewThreadId.Should().Be("t_new");
        evt.Status.Should().Be("running");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabAgentInteractionEnd_ParsesCollabAgentInteractionEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_agent_interaction_end"",
                ""call_id"": ""c2"",
                ""sender_thread_id"": ""t_sender"",
                ""receiver_thread_id"": ""t_recv"",
                ""receiver_agent_nickname"": ""nick2"",
                ""receiver_agent_role"": ""explorer"",
                ""prompt"": ""hi"",
                ""status"": ""completed""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabAgentInteractionEndEvent>().Subject;
        evt.Type.Should().Be("collab_agent_interaction_end");
        evt.CallId.Should().Be("c2");
        evt.ReceiverThreadId.Should().Be("t_recv");
        evt.Status.Should().Be("completed");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabWaitingEnd_ParsesCollabWaitingEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_waiting_end"",
                ""call_id"": ""c3"",
                ""sender_thread_id"": ""t_sender"",
                ""statuses"": {{ ""t1"": ""completed"", ""t2"": ""running"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabWaitingEndEvent>().Subject;
        evt.Type.Should().Be("collab_waiting_end");
        evt.CallId.Should().Be("c3");
        evt.Statuses!.Should().ContainKey("t1").WhoseValue.Should().Be("completed");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabCloseEnd_ParsesCollabCloseEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_close_end"",
                ""call_id"": ""c4"",
                ""sender_thread_id"": ""t_sender"",
                ""receiver_thread_id"": ""t_recv"",
                ""status"": ""shutdown""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabCloseEndEvent>().Subject;
        evt.Type.Should().Be("collab_close_end");
        evt.CallId.Should().Be("c4");
        evt.Status.Should().Be("shutdown");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabResumeEnd_ParsesCollabResumeEndEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_resume_end"",
                ""call_id"": ""c5"",
                ""sender_thread_id"": ""t_sender"",
                ""receiver_thread_id"": ""t_recv"",
                ""status"": ""running""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabResumeEndEvent>().Subject;
        evt.Type.Should().Be("collab_resume_end");
        evt.CallId.Should().Be("c5");
        evt.Status.Should().Be("running");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ExitedReviewMode_ParsesStructuredReviewOutput()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""exited_review_mode"",
                ""review_output"": {{
                    ""overall_correctness"": ""good"",
                    ""overall_explanation"": ""Looks fine."",
                    ""overall_confidence_score"": 0.9,
                    ""findings"": [
                        {{
                            ""priority"": 1,
                            ""confidence_score"": 0.8,
                            ""title"": ""Issue title"",
                            ""body"": ""Issue body"",
                            ""code_location"": {{
                                ""absolute_file_path"": ""/tmp/file.cs"",
                                ""line_range"": {{ ""start"": 10, ""end"": 12 }}
                            }}
                        }}
                    ]
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ExitedReviewModeEvent>().Subject;
        evt.Type.Should().Be("exited_review_mode");
        evt.ReviewOutput.OverallCorrectness.Should().Be("good");
        evt.ReviewOutput.OverallConfidenceScore.Should().Be(0.9);
        evt.ReviewOutput.Findings.Should().HaveCount(1);
        evt.ReviewOutput.Findings[0].CodeLocation!.AbsoluteFilePath.Should().Be("/tmp/file.cs");
        evt.ReviewOutput.Findings[0].CodeLocation!.LineRange!.Start.Should().Be(10);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ExitedReviewMode_WithNestedPayload_ParsesStructuredReviewOutput()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""exited_review_mode"",
                ""payload"": {{
                    ""review_output"": {{
                        ""overall_correctness"": ""good"",
                        ""overall_explanation"": ""Looks fine."",
                        ""overall_confidence_score"": 0.9,
                        ""findings"": [
                            {{
                                ""priority"": 1,
                                ""confidence_score"": 0.8,
                                ""title"": ""Issue title"",
                                ""body"": ""Issue body"",
                                ""code_location"": {{
                                    ""absolute_file_path"": ""/tmp/file.cs"",
                                    ""line_range"": {{ ""start"": 10, ""end"": 12 }}
                                }}
                            }}
                        ]
                    }}
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ExitedReviewModeEvent>().Subject;
        evt.Type.Should().Be("exited_review_mode");
        evt.ReviewOutput.OverallCorrectness.Should().Be("good");
        evt.ReviewOutput.OverallConfidenceScore.Should().Be(0.9);
        evt.ReviewOutput.Findings.Should().HaveCount(1);
        evt.ReviewOutput.Findings[0].CodeLocation!.AbsoluteFilePath.Should().Be("/tmp/file.cs");
        evt.ReviewOutput.Findings[0].CodeLocation!.LineRange!.Start.Should().Be(10);
    }

    [Fact]
    public async Task ParseAsync_UnknownEventType_CreatesUnknownCodexEvent()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var unknownJson = $@"{{
            ""type"": ""unknown_future_event"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""some_field"": ""some_value""
            }}
        }}";
        var lines = AsyncEnumerable.Repeat(unknownJson, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UnknownCodexEvent>().Subject;
        evt.Type.Should().Be("unknown_future_event");
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_SkipsLineAndContinues()
    {
        // Arrange
        var validLine = TestJsonlGenerator.GenerateUserMessage("Valid message");
        var malformedLine = "{ invalid json without closing brace";
        var anotherValidLine = TestJsonlGenerator.GenerateAgentMessage("Another valid message");

        var lines = new[] { validLine, malformedLine, anotherValidLine }.ToAsyncEnumerable();

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<UserMessageEvent>();
        events[1].Should().BeOfType<AgentMessageEvent>();
    }

    [Fact]
    public async Task ParseAsync_EmptyLines_AreSkipped()
    {
        // Arrange
        var validLine = TestJsonlGenerator.GenerateUserMessage("Test message");
        var lines = new[] { "", validLine, "   ", "\t", validLine }.ToAsyncEnumerable();

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(2);
        events.Should().AllBeOfType<UserMessageEvent>();
    }

    [Fact]
    public async Task ParseAsync_MissingTimestampField_SkipsEvent()
    {
        // Arrange
        var invalidJson = @"{
            ""type"": ""user_message"",
            ""payload"": {
                ""message"": ""Test""
            }
        }";
        var lines = AsyncEnumerable.Repeat(invalidJson, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_MissingTypeField_SkipsEvent()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var invalidJson = $@"{{
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""message"": ""Test""
            }}
        }}";
        var lines = AsyncEnumerable.Repeat(invalidJson, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_NullLines_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _parser.ParseAsync(null!).ToListAsync();

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("lines");
    }

    [Fact]
    public async Task ParseAsync_CompleteSession_ParsesAllEvents()
    {
        // Arrange
        var sessionId = SessionId.Parse("complete-session-test");
        var jsonl = TestJsonlGenerator.GenerateSession(
            sessionId,
            "/home/user/project",
            "User question",
            "Agent response",
            includeReasoning: true,
            includeTokens: true);

        var lines = jsonl.Split(Environment.NewLine).ToAsyncEnumerable();

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(6);
        events[0].Should().BeOfType<SessionMetaEvent>();
        events[1].Should().BeOfType<TurnContextEvent>();
        events[2].Should().BeOfType<UserMessageEvent>();
        events[3].Should().BeOfType<AgentReasoningEvent>();
        events[4].Should().BeOfType<AgentMessageEvent>();
        events[5].Should().BeOfType<TokenCountEvent>();
    }

    [Fact]
    public async Task ParseAsync_CancellationToken_StopsProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var infiniteLines = CreateInfiniteLines();

        // Act
        var events = new List<CodexEvent>();
        await foreach (var evt in _parser.ParseAsync(infiniteLines, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 3)
            {
                cts.Cancel();
            }
        }

        // Assert
        events.Should().HaveCount(3);
    }

    private static async IAsyncEnumerable<string> CreateInfiniteLines()
    {
        var index = 0;
        while (true)
        {
            yield return TestJsonlGenerator.GenerateUserMessage($"Message {index++}");
            await Task.Delay(1); // Simulate some delay
        }
    }
}
