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
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

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

        try
        {
            await using var codex = await sdk.McpServer.StartAsync(ct);

            var tools = await codex.ListToolsAsync(ct);
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

            var reply = await codex.ReplyAsync(run.ThreadId, followUp, ct);
            if (!string.IsNullOrEmpty(reply.Text))
            {
                Console.WriteLine(reply.Text);
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
    }
}
