using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerOptOutNotifications;

public sealed class AppServerOptOutNotificationsCommand : AsyncCommand<AppServerOptOutNotificationsSettings>
{
    private static readonly string[] DefaultOptOutMethods =
    [
        // Upstream-defined notification method; see docs/AppServer/README.md.
        "item/agentMessage/delta"
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AppServerOptOutNotificationsSettings settings,
        CancellationToken cancellationToken)
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
            Console.WriteLine("Baseline run (no opt-out)...");
            var baseline = await RunOnceAsync(repoPath, settings, optOutAgentDeltas: false, ct);

            Console.WriteLine();
            Console.WriteLine("Opt-out run (disable agent delta notifications)...");
            var optOut = await RunOnceAsync(repoPath, settings, optOutAgentDeltas: true, ct);

            Console.WriteLine();
            Console.WriteLine($"AgentMessageDeltaNotification count: baseline={baseline}, optOut={optOut}");

            if (baseline <= 0)
            {
                Console.Error.WriteLine("Expected baseline to observe at least one AgentMessageDeltaNotification.");
                return 1;
            }

            if (optOut != 0)
            {
                Console.Error.WriteLine("Expected opt-out run to observe zero AgentMessageDeltaNotification.");
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
    }

    private static async Task<int> RunOnceAsync(
        string repoPath,
        AppServerOptOutNotificationsSettings settings,
        bool optOutAgentDeltas,
        CancellationToken ct)
    {
        var optOutMethods = optOutAgentDeltas ? DefaultOptOutMethods : null;

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
            {
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK AppServer OptOut Demo", "1.0.0");
                o.Capabilities = new InitializeCapabilities
                {
                    ExperimentalApi = true,
                    OptOutNotificationMethods = optOutMethods
                };
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

        await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Input = [TurnInputItem.Text(settings.Prompt)]
        }, ct);

        var deltas = 0;
        await foreach (var ev in turn.Events(ct))
        {
            if (ev is AgentMessageDeltaNotification delta)
            {
                deltas++;
                if (!optOutAgentDeltas)
                {
                    Console.Write(delta.Delta);
                }
            }
        }

        var completed = await turn.Completion;
        Console.WriteLine($"\nDone: {completed.Status}");

        return deltas;
    }
}
