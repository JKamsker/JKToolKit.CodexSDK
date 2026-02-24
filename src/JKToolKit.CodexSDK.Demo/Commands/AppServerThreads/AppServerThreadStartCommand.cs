using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadStartCommand : AsyncCommand<AppServerThreadStartSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadStartSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
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

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                ModelProvider = string.IsNullOrWhiteSpace(settings.ModelProvider) ? null : settings.ModelProvider,
                Cwd = cwd,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox,
                Ephemeral = settings.Ephemeral ? true : null,
                Personality = string.IsNullOrWhiteSpace(settings.Personality) ? null : settings.Personality
            }, ct);

            Console.WriteLine($"Started thread: {thread.Id}");

            if (!string.IsNullOrWhiteSpace(settings.Seed))
            {
                await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
                {
                    Input = [TurnInputItem.Text(settings.Seed)]
                }, ct);

                var completed = await turn.Completion;
                Console.WriteLine($"Seed turn done: {completed.Status}");
            }

            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(thread.Raw);
            }
            return 0;
        });
}

public sealed class AppServerThreadStartSettings : AppServerThreadsSettingsBase
{
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

    [CommandOption("--ephemeral")]
    public bool Ephemeral { get; init; }

    [CommandOption("--seed <TEXT>")]
    public string? Seed { get; init; }
}
