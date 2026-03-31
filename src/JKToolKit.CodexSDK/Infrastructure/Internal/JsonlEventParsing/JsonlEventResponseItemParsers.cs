using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventResponseItemParsers
{
    public static ResponseItemEvent? ParseResponseItemEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            ctx.Logger.LogWarning("response_item event missing 'payload' field");
            return new ResponseItemEvent
            {
                Timestamp = timestamp,
                Type = type,
                RawPayload = rawPayload,
                PayloadType = "unknown",
                Payload = new UnknownResponseItemPayload
                {
                    PayloadType = "unknown",
                    Raw = root.Clone()
                }
            };
        }

        if (payload.ValueKind == JsonValueKind.Array)
        {
            return new ResponseItemEvent
            {
                Timestamp = timestamp,
                Type = type,
                RawPayload = rawPayload,
                PayloadType = "batch",
                Payload = new UnknownResponseItemPayload
                {
                    PayloadType = "batch",
                    Raw = payload.Clone()
                }
            };
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("response_item event has non-object 'payload' field");
            return new ResponseItemEvent
            {
                Timestamp = timestamp,
                Type = type,
                RawPayload = rawPayload,
                PayloadType = "unknown",
                Payload = new UnknownResponseItemPayload
                {
                    PayloadType = "unknown",
                    Raw = payload.Clone()
                }
            };
        }

        var payloadType = TryGetString(payload, "type");

        if (string.IsNullOrWhiteSpace(payloadType))
        {
            ctx.Logger.LogWarning("response_item event missing 'payload.type' field");
            return new ResponseItemEvent
            {
                Timestamp = timestamp,
                Type = type,
                RawPayload = rawPayload,
                PayloadType = "unknown",
                Payload = new UnknownResponseItemPayload
                {
                    PayloadType = "unknown",
                    Raw = payload.Clone()
                }
            };
        }

        var normalized = ParseResponseItemPayload(payloadType, payload);

        return new ResponseItemEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            PayloadType = payloadType,
            Payload = normalized
        };
    }

    public static ResponseItemPayload ParseResponseItemPayload(string payloadType, JsonElement payload)
    {
        if (string.Equals(payloadType, "reasoning", StringComparison.OrdinalIgnoreCase))
        {
            var summaries = Array.Empty<string>();
            if (payload.TryGetProperty("summary", out var summaryArray) && summaryArray.ValueKind == JsonValueKind.Array)
            {
                summaries = summaryArray
                    .EnumerateArray()
                    .Select(s => s.ValueKind == JsonValueKind.Object ? TryGetString(s, "text") : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToArray();
            }

            var encrypted = TryGetString(payload, "encrypted_content");
            var content = ParseReasoningContent(payload);

            return new ReasoningResponseItemPayload
            {
                PayloadType = payloadType,
                SummaryTexts = summaries,
                Content = content,
                EncryptedContent = encrypted
            };
        }

        if (string.Equals(payloadType, "message", StringComparison.OrdinalIgnoreCase))
        {
            var role = TryGetString(payload, "role");
            var phase = TryGetString(payload, "phase");
            var parts = ParseMessageContent(payload);
            return new MessageResponseItemPayload
            {
                PayloadType = payloadType,
                Role = role,
                Phase = phase,
                EndTurn = ParseNullableBoolean(payload, "end_turn"),
                Content = parts
            };
        }

        if (string.Equals(payloadType, "local_shell_call", StringComparison.OrdinalIgnoreCase))
        {
            string? actionType = null;
            IReadOnlyList<string>? command = null;
            long? timeoutMs = null;
            string? workingDirectory = null;
            IReadOnlyDictionary<string, string>? env = null;
            string? user = null;
            JsonElement? actionJson = null;

            if (payload.TryGetProperty("action", out var actionEl) && actionEl.ValueKind == JsonValueKind.Object)
            {
                actionJson = actionEl.Clone();
                actionType = TryGetString(actionEl, "type");

                if (string.Equals(actionType, "exec", StringComparison.OrdinalIgnoreCase))
                {
                    if (actionEl.TryGetProperty("command", out var cmdEl) && cmdEl.ValueKind == JsonValueKind.Array)
                    {
                        command = cmdEl.EnumerateArray()
                            .Select(s => s.ValueKind == JsonValueKind.String
                                ? (s.GetString() ?? string.Empty)
                                : s.GetRawText())
                            .ToArray();
                    }

                    if (actionEl.TryGetProperty("timeout_ms", out var timeoutEl))
                    {
                        timeoutMs = timeoutEl.ValueKind switch
                        {
                            JsonValueKind.Number => timeoutEl.TryGetInt64(out var v) ? v : null,
                            JsonValueKind.String => long.TryParse(timeoutEl.GetString(), out var v) ? v : null,
                            _ => null
                        };
                    }

                    workingDirectory = TryGetString(actionEl, "working_directory");

                    if (actionEl.TryGetProperty("env", out var envEl) && envEl.ValueKind == JsonValueKind.Object)
                    {
                        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var prop in envEl.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                ? (prop.Value.GetString() ?? string.Empty)
                                : prop.Value.GetRawText();
                        }

                        env = dict;
                    }

                    user = TryGetString(actionEl, "user");
                }
            }

            return new LocalShellCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                CallId = TryGetString(payload, "call_id"),
                ActionType = actionType,
                Command = command,
                TimeoutMs = timeoutMs,
                WorkingDirectory = workingDirectory,
                Env = env,
                User = user,
                ActionJson = actionJson
            };
        }

        if (string.Equals(payloadType, "function_call", StringComparison.OrdinalIgnoreCase))
        {
            var name = TryGetString(payload, "name");
            string? argsJson = null;
            JsonElement? arguments = null;
            if (payload.TryGetProperty("arguments", out var argsEl))
            {
                argsJson = argsEl.ValueKind == JsonValueKind.String
                    ? argsEl.GetString()
                    : argsEl.GetRawText();

                if (argsEl.ValueKind != JsonValueKind.String)
                {
                    arguments = argsEl.Clone();
                }
            }
            var callId = TryGetString(payload, "call_id");

            return new FunctionCallResponseItemPayload
            {
                PayloadType = payloadType,
                Name = name,
                Namespace = TryGetString(payload, "namespace"),
                ArgumentsJson = argsJson,
                Arguments = arguments,
                CallId = callId
            };
        }

        if (string.Equals(payloadType, "function_call_output", StringComparison.OrdinalIgnoreCase))
        {
            var callId = TryGetString(payload, "call_id");
            var (output, outputJson) = ParseStringOrStructured(payload, "output");
            var outputContent = ParseFunctionToolOutputContent(outputJson);

            return new FunctionCallOutputResponseItemPayload
            {
                PayloadType = payloadType,
                CallId = callId,
                Output = output,
                OutputJson = outputJson,
                OutputContent = outputContent
            };
        }

        if (string.Equals(payloadType, "custom_tool_call", StringComparison.OrdinalIgnoreCase))
        {
            var (input, inputJson) = ParseStringOrStructured(payload, "input");
            return new CustomToolCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                CallId = TryGetString(payload, "call_id"),
                Name = TryGetString(payload, "name"),
                Input = input,
                InputJson = inputJson
            };
        }

        if (string.Equals(payloadType, "custom_tool_call_output", StringComparison.OrdinalIgnoreCase))
        {
            var (output, outputJson) = ParseStringOrStructured(payload, "output");
            var outputContent = ParseFunctionToolOutputContent(outputJson);
            return new CustomToolCallOutputResponseItemPayload
            {
                PayloadType = payloadType,
                CallId = TryGetString(payload, "call_id"),
                Name = TryGetString(payload, "name"),
                Output = output,
                OutputJson = outputJson,
                OutputContent = outputContent
            };
        }

        if (string.Equals(payloadType, "tool_search_call", StringComparison.OrdinalIgnoreCase))
        {
            JsonElement? arguments = null;
            if (payload.TryGetProperty("arguments", out var argumentsEl))
            {
                arguments = argumentsEl.Clone();
            }

            return new ToolSearchCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                CallId = TryGetString(payload, "call_id"),
                Execution = TryGetString(payload, "execution"),
                Arguments = arguments
            };
        }

        if (string.Equals(payloadType, "tool_search_output", StringComparison.OrdinalIgnoreCase))
        {
            var tools = Array.Empty<JsonElement>();
            if (payload.TryGetProperty("tools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
            {
                tools = toolsEl.EnumerateArray()
                    .Select(tool => tool.Clone())
                    .ToArray();
            }

            return new ToolSearchOutputResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                CallId = TryGetString(payload, "call_id"),
                Execution = TryGetString(payload, "execution"),
                Tools = tools
            };
        }

        if (string.Equals(payloadType, "image_generation_call", StringComparison.OrdinalIgnoreCase))
        {
            return new ImageGenerationCallResponseItemPayload
            {
                PayloadType = payloadType,
                Id = TryGetString(payload, "id"),
                Status = TryGetString(payload, "status"),
                RevisedPrompt = TryGetString(payload, "revised_prompt"),
                Result = TryGetString(payload, "result")
            };
        }

        if (string.Equals(payloadType, "web_search_call", StringComparison.OrdinalIgnoreCase))
        {
            WebSearchAction? action = null;
            if (payload.TryGetProperty("action", out var actionEl))
            {
                action = JsonlEventEnvelopeParsers.ParseWebSearchAction(actionEl);
            }

            return new WebSearchCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                Action = action
            };
        }

        if (string.Equals(payloadType, "ghost_snapshot", StringComparison.OrdinalIgnoreCase))
        {
            GhostCommit? commit = null;
            if (payload.TryGetProperty("ghost_commit", out var commitEl) && commitEl.ValueKind == JsonValueKind.Object)
            {
                IReadOnlyList<string>? files = null;
                if (commitEl.TryGetProperty("preexisting_untracked_files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
                {
                    files = filesEl.EnumerateArray()
                        .Select(f => f.ValueKind == JsonValueKind.String ? f.GetString() : null)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Cast<string>()
                        .ToArray();
                }

                IReadOnlyList<string>? dirs = null;
                if (commitEl.TryGetProperty("preexisting_untracked_dirs", out var dirsEl) && dirsEl.ValueKind == JsonValueKind.Array)
                {
                    dirs = dirsEl.EnumerateArray()
                        .Select(d => d.ValueKind == JsonValueKind.String ? d.GetString() : null)
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .Cast<string>()
                        .ToArray();
                }

                commit = new GhostCommit(
                    Id: TryGetString(commitEl, "id"),
                    Parent: TryGetString(commitEl, "parent"),
                    PreexistingUntrackedFiles: files,
                    PreexistingUntrackedDirs: dirs);
            }

            return new GhostSnapshotResponseItemPayload
            {
                PayloadType = payloadType,
                GhostCommit = commit
            };
        }

        if (string.Equals(payloadType, "compaction", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(payloadType, "compaction_summary", StringComparison.OrdinalIgnoreCase))
        {
            return new CompactionResponseItemPayload
            {
                PayloadType = payloadType,
                EncryptedContent = TryGetString(payload, "encrypted_content")
            };
        }

        return new UnknownResponseItemPayload
        {
            PayloadType = payloadType,
            Raw = payload.Clone()
        };
    }

}
