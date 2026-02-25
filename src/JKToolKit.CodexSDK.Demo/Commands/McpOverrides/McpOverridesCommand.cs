using System.Text.Json;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.McpServer.Overrides;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.McpOverrides;

public sealed class McpOverridesCommand : AsyncCommand<McpOverridesSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, McpOverridesSettings settings, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        var workDir = Path.Combine(Path.GetTempPath(), "codexsdk-mcp-overrides-work-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workDir);

        var elicitation = new AutoApproveElicitationHandler(maxLogLines: 5);
        var toolTransformer = new CodexToolResultMarkerTransformer(maxLogLines: 5);
        var toolMapper = new CodexToolResultMarkerMapper(maxLogLines: 5);

        var opts = new CodexMcpServerClientOptions
        {
            CodexExecutablePath = settings.CodexExecutablePath,
            CodexHomeDirectory = settings.CodexHomeDirectory,
            ElicitationHandler = elicitation,
            CodexToolResultTransformers = new ICodexMcpToolResultTransformer[] { toolTransformer },
            CodexToolResultMappers = new ICodexMcpToolResultMapper[] { toolMapper }
        };

        try
        {
            Console.WriteLine($"CODEX_HOME: {settings.CodexHomeDirectory ?? "<default>"}");
            Console.WriteLine($"Work dir:   {workDir}");
            Console.WriteLine();

            await using var client = await CodexMcpServerClient.StartAsync(opts, ct);

            var tools = await client.ListToolsAsync(ct);
            Console.WriteLine("Tools:");
            foreach (var tool in tools)
            {
                Console.WriteLine($"- {tool.Name}");
            }

            if (!tools.Any(t => string.Equals(t.Name, "codex", StringComparison.Ordinal)))
            {
                Console.Error.WriteLine("Expected tools/list to include 'codex'.");
                return 1;
            }

            var prompt =
                "Run the exact command `git init .` in the current working directory, then reply with 'done' and nothing else.";

            var start = await client.StartSessionAsync(new CodexMcpStartOptions
            {
                Prompt = prompt,
                Cwd = workDir,
                Sandbox = CodexSandboxMode.WorkspaceWrite,
                ApprovalPolicy = CodexApprovalPolicy.Untrusted,
                Model = CodexModel.Gpt52Codex
            }, ct);

            if (!string.IsNullOrWhiteSpace(start.Text))
            {
                Console.WriteLine();
                Console.WriteLine(start.Text);
            }

            var gitDir = Path.Combine(workDir, ".git");
            var gitInitialized = Directory.Exists(gitDir);

            Console.WriteLine();
            Console.WriteLine($".git exists: {gitInitialized}");
            Console.WriteLine($"Elicitation requests observed: {elicitation.CallCount}");
            Console.WriteLine($"ToolResultTransformer calls:   {toolTransformer.CallCount}");
            Console.WriteLine($"ToolResultMapper calls:        {toolMapper.CallCount}");

            if (!gitInitialized)
            {
                Console.Error.WriteLine("Expected `git init .` to create a .git directory under the work dir.");
                return 1;
            }

            if (elicitation.CallCount <= 0)
            {
                Console.Error.WriteLine("Expected at least one server-initiated elicitation request to be handled.");
                return 1;
            }

            if (toolTransformer.CallCount <= 0 || toolMapper.CallCount <= 0)
            {
                Console.Error.WriteLine("Expected Codex tool result override hooks (transformer + mapper) to be invoked.");
                return 1;
            }

            Console.WriteLine("ok");
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // ignore
        }
    }

    private sealed class AutoApproveElicitationHandler : IMcpElicitationHandler
    {
        private readonly int _maxLogLines;
        private int _logged;

        public int CallCount { get; private set; }

        public AutoApproveElicitationHandler(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
        {
            CallCount++;

            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[elicitation] {method} paramsKind={@params?.ValueKind.ToString() ?? "null"}");
                if (@params is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                {
                    Console.WriteLine($"[elicitation] message: {msg.GetString()}");
                }

                if (string.Equals(method, "elicitation/create", StringComparison.OrdinalIgnoreCase) && @params is { ValueKind: JsonValueKind.Object } p2)
                {
                    Console.WriteLine($"[elicitation] params: {Truncate(p2.GetRawText(), maxChars: 1200)}");
                }
            }

            // Best-effort: Codex MCP may use either MCP-standard elicitation or Codex-specific approval requests.
            if (string.Equals(method, "elicitation/create", StringComparison.OrdinalIgnoreCase))
            {
                // Upstream codex-rs currently expects an ExecApprovalResponse shape:
                //   { "decision": "approved" }
                // even though the MCP elicitation spec calls for { "action": ..., "content": ... }.
                if (@params is { ValueKind: JsonValueKind.Object } p &&
                    p.TryGetProperty("codex_elicitation", out var kind) &&
                    kind.ValueKind == JsonValueKind.String &&
                    string.Equals(kind.GetString(), "exec-approval", StringComparison.OrdinalIgnoreCase))
                {
                    var ok = JsonSerializer.SerializeToElement(new { decision = "approved" });
                    return ValueTask.FromResult(ok);
                }

                var fallbackElicitation = JsonSerializer.SerializeToElement(new { decision = "approved" });
                return ValueTask.FromResult(fallbackElicitation);
            }

            if (method.EndsWith("Approval", StringComparison.OrdinalIgnoreCase))
            {
                var ok = JsonSerializer.SerializeToElement(new { decision = "approved" });
                return ValueTask.FromResult(ok);
            }

            // Generic fallback: accept.
            var fallback = JsonSerializer.SerializeToElement(new { decision = "approved" });
            return ValueTask.FromResult(fallback);
        }

        private static string Truncate(string s, int maxChars) =>
            s.Length <= maxChars ? s : s.Substring(0, maxChars) + " ...<truncated>";
    }

    private sealed class CodexToolResultMarkerTransformer : ICodexMcpToolResultTransformer
    {
        private readonly int _maxLogLines;
        private int _logged;

        public int CallCount { get; private set; }

        public CodexToolResultMarkerTransformer(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public JsonElement Transform(string toolName, JsonElement raw)
        {
            CallCount++;
            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[codex-tool-result-transformer] tool={toolName} kind={raw.ValueKind}");
            }
            return raw;
        }
    }

    private sealed class CodexToolResultMarkerMapper : ICodexMcpToolResultMapper
    {
        private readonly int _maxLogLines;
        private int _logged;

        public int CallCount { get; private set; }

        public CodexToolResultMarkerMapper(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public CodexMcpToolParsedResult? TryMap(string toolName, JsonElement raw)
        {
            CallCount++;
            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[codex-tool-result-mapper] tool={toolName} kind={raw.ValueKind}");
            }
            return null;
        }
    }
}
