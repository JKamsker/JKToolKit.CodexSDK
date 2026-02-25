using System.Text.Json;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.McpServer;

public sealed class McpServerCommand : AsyncCommand<McpServerSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, McpServerSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = settings.RepoPath ?? Directory.GetCurrentDirectory();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        try
        {
            Console.CancelKeyPress += cancelHandler;

            var model = string.IsNullOrWhiteSpace(settings.Model)
                ? CodexModel.Gpt52Codex
                : CodexModel.Parse(settings.Model);

            var approvalPolicy = string.IsNullOrWhiteSpace(settings.ApprovalPolicy)
                ? CodexApprovalPolicy.Never
                : CodexApprovalPolicy.Parse(settings.ApprovalPolicy);

            var sandbox = string.IsNullOrWhiteSpace(settings.Sandbox)
                ? CodexSandboxMode.WorkspaceWrite
                : CodexSandboxMode.Parse(settings.Sandbox);

            var prompt = string.IsNullOrWhiteSpace(settings.Prompt)
                ? "Run tests and summarize failures."
                : settings.Prompt;

            var followUp = string.IsNullOrWhiteSpace(settings.FollowUp)
                ? "Now propose fixes."
                : settings.FollowUp;

            await using var sdk = CodexSdk.Create(builder =>
            {
                builder.CodexExecutablePath = settings.CodexExecutablePath;
                builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            });

            await using var codex = await sdk.McpServer.StartAsync(ct);

            IReadOnlyList<McpToolDescriptor> tools;
            if (settings.UseLowLevelCalls)
            {
                var rawTools = await codex.CallAsync("tools/list", @params: null, ct);
                tools = ParseToolsList(rawTools);
                Console.WriteLine($"[low-level] CallAsync(tools/list): tools={tools.Count}");
            }
            else
            {
                tools = await codex.ListToolsAsync(ct);
            }

            Console.WriteLine("Tools:");
            foreach (var tool in tools)
            {
                Console.WriteLine($"- {tool.Name}");
            }

            var run = await codex.StartSessionAsync(new CodexMcpStartOptions
            {
                Prompt = prompt,
                Cwd = repoPath,
                Sandbox = sandbox,
                ApprovalPolicy = approvalPolicy,
                Model = model,
                IncludePlanTool = settings.IncludePlanTool ? true : null
            }, ct);

            if (!string.IsNullOrEmpty(run.Text))
            {
                Console.WriteLine(run.Text);
            }

            if (settings.UseLowLevelCalls)
            {
                var call = await codex.CallToolAsync(
                    "codex-reply",
                    new Dictionary<string, object?>
                    {
                        ["threadId"] = run.ThreadId,
                        ["prompt"] = followUp
                    },
                    ct);

                var text = CodexMcpToolResultParsers.TryExtractText(call.Raw);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine(text);
                }
            }
            else
            {
                var reply = await codex.ReplyAsync(run.ThreadId, followUp, ct);
                if (!string.IsNullOrEmpty(reply.Text))
                {
                    Console.WriteLine(reply.Text);
                }
            }

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
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static IReadOnlyList<McpToolDescriptor> ParseToolsList(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("tools", out var toolsProp) ||
            toolsProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<McpToolDescriptor>();
        }

        var list = new List<McpToolDescriptor>();
        foreach (var tool in toolsProp.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = tool.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var description = tool.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            JsonElement? schema = null;
            if (tool.TryGetProperty("inputSchema", out var schemaProp))
            {
                schema = schemaProp.Clone();
            }

            list.Add(new McpToolDescriptor(name, description, schema));
        }

        return list;
    }
}
