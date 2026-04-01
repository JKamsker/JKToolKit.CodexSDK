using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerCollaborationMode;

public sealed class AppServerCollaborationModeCommand : AsyncCommand<AppServerCollaborationModeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerCollaborationModeSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);

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

        try
        {
            await ValidateGuardAsync(repoPath, settings, ct);
            await ValidateExperimentalE2EAsync(repoPath, settings, ct);
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
    }

    private static async Task ValidateGuardAsync(string repoPath, AppServerCollaborationModeSettings settings, CancellationToken ct)
    {
        Console.WriteLine("Phase 1: stable-only guard (expected failure)...");

        var collab = AppServerCollaborationModePayloadBuilder.BuildCollaborationModeJson(new CollaborationModeMaskProjection
        {
            Mode = "plan",
            Model = CodexModel.Gpt52Codex.Value,
            IncludesDeveloperInstructions = true
        });

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
            {
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK CollaborationMode Guard Demo", "1.0.0");
                o.ExperimentalApi = false;
            });
        });

        await using var codex = await sdk.AppServer.StartAsync(ct);
        var thread = await codex.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt52Codex,
            Cwd = repoPath,
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.WorkspaceWrite
        }, ct);

        try
        {
            await using var _ = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text("This should throw before sending.")],
                CollaborationMode = collab
            }, ct);

            throw new InvalidOperationException("Expected CodexExperimentalApiRequiredException for turn/start.collaborationMode.");
        }
        catch (CodexExperimentalApiRequiredException ex) when (ex.Descriptor == "turn/start.collaborationMode")
        {
            Console.WriteLine($"[expected] {ex.GetType().Name} Descriptor='{ex.Descriptor}'");
        }
    }

    private static async Task ValidateExperimentalE2EAsync(string repoPath, AppServerCollaborationModeSettings settings, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("Phase 2: experimental API (collaborationMode/list + turn/start.collaborationMode)...");

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
            {
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK CollaborationMode E2E Demo", "1.0.0");
                o.ExperimentalApi = true;
            });
        });

        await using var codex = await sdk.AppServer.StartAsync(ct);

        var list = await codex.CallAsync("collaborationMode/list", new { }, ct);
        if (!AppServerCollaborationModePayloadBuilder.TryGetFirstMask(list, out var mask))
        {
            throw new InvalidOperationException($"Unexpected collaborationMode/list result shape: {list}");
        }

        Console.WriteLine($"Preset: mode={mask.Mode}, model={mask.Model}, effort={mask.ReasoningEffort ?? "n/a"}");

        var collab = AppServerCollaborationModePayloadBuilder.BuildCollaborationModeJson(mask);

        var thread = await codex.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt52Codex,
            Cwd = repoPath,
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.WorkspaceWrite
        }, ct);

        Console.WriteLine($"Thread: {thread.Id}");
        Console.WriteLine($"> {settings.Prompt}");
        Console.WriteLine();

        await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Input = [TurnInputItem.Text(settings.Prompt)],
            CollaborationMode = collab
        }, ct);

        await foreach (var ev in turn.Events(ct))
        {
            if (ev is AgentMessageDeltaNotification delta)
            {
                Console.Write(delta.Delta);
            }
        }

        var completed = await turn.Completion;
        Console.WriteLine($"\nDone: {completed.Status}");
    }

}
