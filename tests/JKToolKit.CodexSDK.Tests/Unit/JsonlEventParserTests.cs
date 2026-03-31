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
    public async Task ParseAsync_SessionMetaEvent_WithSubagentThreadSpawnSource_ParsesSourceAndSubagentKind()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""session_meta"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""id"": ""019d45db-8fab-7781-8fc6-9415a9169898"",
                ""cwd"": ""C:\\\\repo"",
                ""source"": {{
                    ""subagent"": {{
                        ""thread_spawn"": {{
                            ""parent_thread_id"": ""019d459c-2019-7b62-b455-c6b4328c9a76"",
                            ""depth"": 1,
                            ""agent_nickname"": ""Halley"",
                            ""agent_role"": ""worker""
                        }}
                    }}
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<SessionMetaEvent>().Subject;
        evt.Source.Should().Be("subagent");
        evt.SourceSubagent.Should().Be("thread_spawn");
    }

    [Fact]
    public async Task ParseAsync_SessionMetaEvent_PreservesStructuredSourceAndAdditionalFields()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""session_meta"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""id"": ""019d45db-8fab-7781-8fc6-9415a9169898"",
                ""forked_from_id"": ""019d45db-8fab-7781-8fc6-9415a9169800"",
                ""cwd"": ""C:\\\\repo"",
                ""originator"": ""codex_cli_rs"",
                ""cli_version"": ""0.117.0"",
                ""source"": {{
                    ""subagent"": {{
                        ""thread_spawn"": {{
                            ""parent_thread_id"": ""019d459c-2019-7b62-b455-c6b4328c9a76"",
                            ""depth"": 1
                        }}
                    }}
                }},
                ""agent_nickname"": ""Maxwell"",
                ""agent_role"": ""explorer"",
                ""agent_path"": ""agents/explorer"",
                ""model_provider"": ""codex-lb"",
                ""base_instructions"": {{ ""text"": ""base"" }},
                ""dynamic_tools"": [{{ ""name"": ""tool_a"" }}],
                ""memory_mode"": ""project"",
                ""git"": {{ ""branch"": ""main"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<SessionMetaEvent>().Subject;
        evt.ForkedFromSessionId.Should().Be(SessionId.Parse("019d45db-8fab-7781-8fc6-9415a9169800"));
        evt.Source.Should().Be("subagent");
        evt.SourceSubagent.Should().Be("thread_spawn");
        evt.SourceJson.HasValue.Should().BeTrue();
        evt.SourceJson!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        evt.AgentNickname.Should().Be("Maxwell");
        evt.AgentRole.Should().Be("explorer");
        evt.AgentPath.Should().Be("agents/explorer");
        evt.BaseInstructions.HasValue.Should().BeTrue();
        evt.DynamicTools.HasValue.Should().BeTrue();
        evt.Git.HasValue.Should().BeTrue();
        evt.MemoryMode.Should().Be("project");
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
        var approvalPolicy = "on-request";
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
    public async Task ParseAsync_TurnContextEvent_WithExternalSandboxNetworkAccess_ParsesNormalizedMode()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""turn_context"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""approval_policy"": ""never"",
                ""sandbox_policy"": {{
                    ""type"": ""external-sandbox"",
                    ""external_sandbox"": {{
                        ""network_access"": ""enabled""
                    }}
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TurnContextEvent>().Subject;
        evt.SandboxPolicyType.Should().Be("external-sandbox");
        evt.NetworkAccess.Should().BeTrue();
        evt.NetworkAccessMode.Should().Be("enabled");
        evt.SandboxPolicyJson.HasValue.Should().BeTrue();
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
    public async Task ParseAsync_EventMsg_WrappedAgentReasoning_AllowsEmptyText()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""agent_reasoning"",
                ""text"": """"
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentReasoningEvent>().Subject;
        evt.Type.Should().Be("agent_reasoning");
        evt.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_EventMsg_WrappedUserMessage_WithImagesAndEmptyMessage_ParsesAsEmptyText()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""user_message"",
                ""message"": """",
                ""images"": [""data:image/png;base64,AAA="" ],
                ""local_images"": [],
                ""text_elements"": []
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UserMessageEvent>().Subject;
        evt.Type.Should().Be("user_message");
        evt.Text.Should().BeEmpty();
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
        evt.StatusInfo!.Status.Should().Be(CollabAgentStatus.Running);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabAgentSpawnEnd_WithCompletedUnionObject_ParsesStatusUnion()
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
                ""status"": {{ ""completed"": null }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabAgentSpawnEndEvent>().Subject;
        evt.Status.Should().Be("completed");
        evt.StatusInfo.Should().NotBeNull();
        evt.StatusInfo!.Status.Should().Be(CollabAgentStatus.Completed);
        evt.StatusInfo.PayloadText.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabAgentSpawnBegin_ParsesSpawnBeginEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_agent_spawn_begin"",
                ""call_id"": ""c1"",
                ""sender_thread_id"": ""t_sender"",
                ""prompt"": ""go"",
                ""model"": ""gpt-5.4"",
                ""reasoning_effort"": ""high""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabAgentSpawnBeginEvent>().Subject;
        evt.CallId.Should().Be("c1");
        evt.SenderThreadId.Should().Be("t_sender");
        evt.Model.Should().Be("gpt-5.4");
        evt.ReasoningEffort.Should().Be("high");
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
    public async Task ParseAsync_EventMsg_CollabAgentInteractionEnd_WithCompletedUnionObject_ParsesStatusUnion()
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
                ""status"": {{ ""completed"": ""done"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabAgentInteractionEndEvent>().Subject;
        evt.Status.Should().Be("completed");
        evt.StatusInfo.Should().NotBeNull();
        evt.StatusInfo!.Status.Should().Be(CollabAgentStatus.Completed);
        evt.StatusInfo.PayloadText.Should().Be("done");
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
    public async Task ParseAsync_EventMsg_CollabWaitingBegin_ParsesReceiverMetadata()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_waiting_begin"",
                ""call_id"": ""c3"",
                ""sender_thread_id"": ""t_sender"",
                ""receiver_thread_ids"": [""t1"", ""t2""],
                ""receiver_agents"": [
                    {{ ""thread_id"": ""t1"", ""agent_nickname"": ""Ada"", ""agent_role"": ""reviewer"" }}
                ]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabWaitingBeginEvent>().Subject;
        evt.ReceiverThreadIds.Should().ContainInOrder("t1", "t2");
        evt.ReceiverAgents.Should().ContainSingle();
        evt.ReceiverAgents![0].AgentNickname.Should().Be("Ada");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabWaitingEnd_PreservesAgentStatusMetadataAndInterruptedPayloadText()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""collab_waiting_end"",
                ""call_id"": ""c3"",
                ""sender_thread_id"": ""t_sender"",
                ""statuses"": {{
                    ""t1"": {{ ""interrupted"": {{ ""text"": ""paused"", ""reason"": ""user"" }} }},
                    ""t2"": ""running""
                }},
                ""agent_statuses"": [
                    {{
                        ""thread_id"": ""t1"",
                        ""agent_nickname"": ""Ada"",
                        ""agent_role"": ""reviewer"",
                        ""status"": {{ ""interrupted"": {{ ""text"": ""paused"", ""reason"": ""user"" }} }}
                    }}
                ]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabWaitingEndEvent>().Subject;
        evt.Statuses!["t1"].Should().Be("interrupted");
        evt.StatusInfos!["t1"].Status.Should().Be(CollabAgentStatus.Interrupted);
        evt.StatusInfos["t1"].PayloadText.Should().Be("paused");
        evt.AgentStatuses.Should().ContainSingle();
        evt.AgentStatuses![0].AgentNickname.Should().Be("Ada");
        evt.AgentStatuses[0].StatusInfo.PayloadText.Should().Be("paused");
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
        evt.Status.Should().Be(CollabReceiverStatus.Shutdown);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabCloseEnd_WithCompletedUnionObject_ParsesCollabReceiverStatus()
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
                ""status"": {{ ""completed"": ""done"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabCloseEndEvent>().Subject;
        evt.Status.Should().Be(CollabReceiverStatus.Completed);
        evt.StatusInfo!.PayloadText.Should().Be("done");
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
        evt.Status.Should().Be(CollabReceiverStatus.Running);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabResumeEnd_WithErroredUnionObject_ParsesCollabReceiverStatus()
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
                ""status"": {{ ""errored"": ""boom"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabResumeEndEvent>().Subject;
        evt.Status.Should().Be(CollabReceiverStatus.Errored);
        evt.StatusInfo!.PayloadText.Should().Be("boom");
    }

    [Fact]
    public async Task ParseAsync_EventMsg_CollabResumeEnd_WithInterruptedUnionObject_ParsesInterruptedStatusAndPayloadText()
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
                ""status"": {{ ""interrupted"": {{ ""text"": ""stopped"" }} }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<CollabResumeEndEvent>().Subject;
        evt.Status.Should().Be(CollabReceiverStatus.Interrupted);
        evt.StatusInfo!.PayloadText.Should().Be("stopped");
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
        evt.ReviewOutput.Should().NotBeNull();
        evt.ReviewOutput!.OverallCorrectness.Should().Be("good");
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
        evt.ReviewOutput.Should().NotBeNull();
        evt.ReviewOutput!.OverallCorrectness.Should().Be("good");
        evt.ReviewOutput.OverallConfidenceScore.Should().Be(0.9);
        evt.ReviewOutput.Findings.Should().HaveCount(1);
        evt.ReviewOutput.Findings[0].CodeLocation!.AbsoluteFilePath.Should().Be("/tmp/file.cs");
        evt.ReviewOutput.Findings[0].CodeLocation!.LineRange!.Start.Should().Be(10);
    }

    [Fact]
    public async Task ParseAsync_EventMsg_ExitedReviewMode_AllowsNullReviewOutput()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""exited_review_mode"",
                ""review_output"": null
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ExitedReviewModeEvent>().Subject;
        evt.Type.Should().Be("exited_review_mode");
        evt.ReviewOutput.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_EventMsg_PlanUpdate_ParsesExplanation()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""event_msg"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""plan_update"",
                ""name"": ""Build plan"",
                ""explanation"": ""Added a new step."",
                ""plan"": [
                    {{ ""step"": ""Do thing"", ""status"": ""in_progress"" }}
                ]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<PlanUpdateEvent>().Subject;
        evt.Type.Should().Be("plan_update");
        evt.Name.Should().Be("Build plan");
        evt.Explanation.Should().Be("Added a new step.");
        evt.Plan.Should().HaveCount(1);
        evt.Plan[0].Step.Should().Be("Do thing");
        evt.Plan[0].Status.Should().Be("in_progress");
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
    public async Task ParseAsync_NonStringType_DoesNotThrow_AndMapsUnknownEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": 123,
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{ ""message"": ""hello"" }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UnknownCodexEvent>().Subject;
        evt.Type.Should().Be("123");
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_PayloadArray_IsPreservedAsBatchUnknownPayload()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": [
                {{ ""type"": ""message"", ""role"": ""assistant"", ""content"": [] }}
            ]
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ResponseItemEvent>().Subject;
        evt.PayloadType.Should().Be("batch");
        evt.Payload.Should().BeOfType<UnknownResponseItemPayload>();
        evt.Payload.As<UnknownResponseItemPayload>().Raw.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_PayloadTypeNonString_DoesNotDropEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{ ""type"": 456, ""content"": [] }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<ResponseItemEvent>().Subject;
        evt.PayloadType.Should().Be("unknown");
        evt.Payload.Should().BeOfType<UnknownResponseItemPayload>();
        evt.Payload.As<UnknownResponseItemPayload>().Raw.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_Message_PreservesPhaseAndEndTurn()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""message"",
                ""role"": ""assistant"",
                ""phase"": ""final_answer"",
                ""end_turn"": true,
                ""content"": [{{ ""type"": ""output_text"", ""text"": ""Final answer."" }}]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<MessageResponseItemPayload>().Subject;

        payload.Phase.Should().Be("final_answer");
        payload.EndTurn.Should().BeTrue();
        payload.TextParts.Should().ContainSingle("Final answer.");
    }

    [Fact]
    public async Task ParseAsync_Compacted_ReplacementHistory_PreservesMessagePhaseAndEndTurn()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""compacted"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""message"": ""compacted"",
                ""replacement_history"": [
                    {{
                        ""type"": ""message"",
                        ""role"": ""assistant"",
                        ""phase"": ""commentary"",
                        ""end_turn"": false,
                        ""content"": [{{ ""type"": ""output_text"", ""text"": ""working..."" }}]
                    }}
                ]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var compacted = events[0].Should().BeOfType<CompactedEvent>().Subject;
        compacted.ReplacementHistory.Should().HaveCount(1);

        var message = compacted.ReplacementHistory[0].Should().BeOfType<MessageResponseItemPayload>().Subject;
        message.Phase.Should().Be("commentary");
        message.EndTurn.Should().BeFalse();
        message.TextParts.Should().ContainSingle("working...");
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_FunctionCall_PreservesNamespace()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""function_call"",
                ""name"": ""fetch_issues"",
                ""namespace"": ""github"",
                ""arguments"": {{ ""repo"": ""octo/test"" }},
                ""call_id"": ""call_123""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<FunctionCallResponseItemPayload>().Subject;

        payload.Name.Should().Be("fetch_issues");
        payload.Namespace.Should().Be("github");
        payload.CallId.Should().Be("call_123");
        payload.Arguments.HasValue.Should().BeTrue();
        payload.Arguments!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_FunctionCallOutput_PreservesStructuredOutputBody()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""function_call_output"",
                ""call_id"": ""call_abc"",
                ""output"": [
                    {{ ""type"": ""input_text"", ""text"": ""done"" }},
                    {{ ""type"": ""input_image"", ""image_url"": ""https://example.com/img.png"" }}
                ]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<FunctionCallOutputResponseItemPayload>().Subject;

        payload.CallId.Should().Be("call_abc");
        payload.Output.Should().Contain("input_text");
        payload.OutputJson.HasValue.Should().BeTrue();
        payload.OutputJson!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        payload.OutputContent.Should().HaveCount(2);
        payload.OutputContent![0].Should().BeOfType<FunctionToolOutputInputTextPart>();
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_CustomToolCallOutput_PreservesNameAndStructuredOutputBody()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""custom_tool_call_output"",
                ""call_id"": ""call_custom"",
                ""name"": ""my_tool"",
                ""output"": {{
                    ""ok"": true,
                    ""count"": 2
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<CustomToolCallOutputResponseItemPayload>().Subject;

        payload.CallId.Should().Be("call_custom");
        payload.Name.Should().Be("my_tool");
        payload.Output.Should().Contain("\"ok\": true");
        payload.OutputJson.HasValue.Should().BeTrue();
        payload.OutputJson!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_CustomToolCall_PreservesStructuredInputBody()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""custom_tool_call"",
                ""status"": ""completed"",
                ""call_id"": ""call_custom"",
                ""name"": ""my_tool"",
                ""input"": {{
                    ""ok"": true,
                    ""count"": 2
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<CustomToolCallResponseItemPayload>().Subject;

        payload.Input.Should().Contain("\"ok\": true");
        payload.InputJson.HasValue.Should().BeTrue();
        payload.InputJson!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_Reasoning_PreservesStructuredContent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""reasoning"",
                ""summary"": [{{ ""type"": ""summary_text"", ""text"": ""summary"" }}],
                ""content"": [
                    {{ ""type"": ""reasoning_text"", ""text"": ""step 1"" }},
                    {{ ""type"": ""text"", ""text"": ""step 2"" }}
                ],
                ""encrypted_content"": null
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<ReasoningResponseItemPayload>().Subject;

        payload.SummaryTexts.Should().ContainSingle("summary");
        payload.Content.Should().HaveCount(2);
        payload.Content[0].Should().BeOfType<ReasoningTextContentPart>()
            .Which.Text.Should().Be("step 1");
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_LocalShellCall_PreservesExactArgvIncludingEmptyEntries()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""local_shell_call"",
                ""call_id"": ""call_1"",
                ""status"": ""completed"",
                ""action"": {{
                    ""type"": ""exec"",
                    ""command"": [""bash"", """", ""  "", ""-lc"", ""echo hi""],
                    ""timeout_ms"": 123,
                    ""working_directory"": ""/tmp"",
                    ""env"": {{ ""A"": ""B"" }},
                    ""user"": ""root""
                }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<LocalShellCallResponseItemPayload>().Subject;

        payload.Command.Should().ContainInOrder("bash", string.Empty, "  ", "-lc", "echo hi");
        payload.ActionJson.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_ToolSearchCall_ParsesTypedPayload()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""tool_search_call"",
                ""call_id"": ""search_1"",
                ""status"": ""in_progress"",
                ""execution"": ""scan_tools"",
                ""arguments"": {{ ""query"": ""git"" }}
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<ToolSearchCallResponseItemPayload>().Subject;

        payload.CallId.Should().Be("search_1");
        payload.Status.Should().Be("in_progress");
        payload.Execution.Should().Be("scan_tools");
        payload.Arguments.HasValue.Should().BeTrue();
        payload.Arguments!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_ToolSearchOutput_ParsesTypedPayload()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""tool_search_output"",
                ""call_id"": ""search_1"",
                ""status"": ""completed"",
                ""execution"": ""scan_tools"",
                ""tools"": [
                    {{ ""name"": ""grep"" }},
                    {{ ""name"": ""sed"" }}
                ]
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<ToolSearchOutputResponseItemPayload>().Subject;

        payload.CallId.Should().Be("search_1");
        payload.Status.Should().Be("completed");
        payload.Execution.Should().Be("scan_tools");
        payload.Tools.Should().HaveCount(2);
        payload.Tools[0].ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_ResponseItem_ImageGenerationCall_ParsesTypedPayload()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var json = $@"{{
            ""type"": ""response_item"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""type"": ""image_generation_call"",
                ""id"": ""img_1"",
                ""status"": ""completed"",
                ""revised_prompt"": ""a cat with a hat"",
                ""result"": ""https://example.com/cat.png""
            }}
        }}";

        var events = await _parser.ParseAsync(AsyncEnumerable.Repeat(json, 1)).ToListAsync();

        events.Should().HaveCount(1);
        var payload = events[0].Should().BeOfType<ResponseItemEvent>().Subject.Payload
            .Should().BeOfType<ImageGenerationCallResponseItemPayload>().Subject;

        payload.Id.Should().Be("img_1");
        payload.Status.Should().Be("completed");
        payload.RevisedPrompt.Should().Be("a cat with a hat");
        payload.Result.Should().Be("https://example.com/cat.png");
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
