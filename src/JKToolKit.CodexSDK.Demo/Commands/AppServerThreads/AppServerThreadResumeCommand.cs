using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadResumeCommand : AsyncCommand<AppServerThreadResumeSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadResumeSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId) && string.IsNullOrWhiteSpace(settings.Path))
            {
                Console.Error.WriteLine("Specify --thread or --path.");
                return 1;
            }

            var model = string.IsNullOrWhiteSpace(settings.Model)
                ? CodexModel.Gpt52Codex
                : CodexModel.Parse(settings.Model);

            var approvalPolicy = string.IsNullOrWhiteSpace(settings.ApprovalPolicy)
                ? CodexApprovalPolicy.Never
                : CodexApprovalPolicy.Parse(settings.ApprovalPolicy);

            var sandbox = string.IsNullOrWhiteSpace(settings.Sandbox)
                ? CodexSandboxMode.WorkspaceWrite
                : CodexSandboxMode.Parse(settings.Sandbox);

            var cwd = string.IsNullOrWhiteSpace(settings.Cwd)
                ? AppServerThreadCommandHelpers.ResolveRepoPath(settings)
                : settings.Cwd;

            var thread = await codex.ResumeThreadAsync(new ThreadResumeOptions
            {
                ThreadId = string.IsNullOrWhiteSpace(settings.ThreadId) ? null : settings.ThreadId,
                Path = string.IsNullOrWhiteSpace(settings.Path) ? null : settings.Path,
                Model = model,
                ModelProvider = string.IsNullOrWhiteSpace(settings.ModelProvider) ? null : settings.ModelProvider,
                Cwd = cwd,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox,
                Personality = string.IsNullOrWhiteSpace(settings.Personality) ? null : settings.Personality
            }, ct);

            Console.WriteLine($"Resumed thread: {thread.Id}");
            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(thread.Raw);
            }
            return 0;
        });
}

public sealed class AppServerThreadResumeSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string? ThreadId { get; init; }

    [CommandOption("--path <PATH>")]
    public string? Path { get; init; }

    [CommandOption("--cwd <DIR>")]
    public string? Cwd { get; init; }

    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--model-provider <ID>")]
    public string? ModelProvider { get; init; }

    [CommandOption("--approval-policy <POLICY>")]
    public string? ApprovalPolicy { get; init; }

    [CommandOption("--sandbox <MODE>")]
    public string? Sandbox { get; init; }

    [CommandOption("--personality <ID>")]
    public string? Personality { get; init; }
}

