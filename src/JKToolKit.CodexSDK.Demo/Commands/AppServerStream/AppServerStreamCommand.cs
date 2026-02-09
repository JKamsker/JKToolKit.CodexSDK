using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerStream;

public sealed class AppServerStreamCommand : AsyncCommand<AppServerStreamSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerStreamSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = settings.RepoPath ?? Directory.GetCurrentDirectory();

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

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? CodexModel.Gpt52Codex
            : CodexModel.Parse(settings.Model);

        var approvalPolicy = string.IsNullOrWhiteSpace(settings.ApprovalPolicy)
            ? CodexApprovalPolicy.Never
            : CodexApprovalPolicy.Parse(settings.ApprovalPolicy);

        var sandbox = string.IsNullOrWhiteSpace(settings.Sandbox)
            ? CodexSandboxMode.WorkspaceWrite
            : CodexSandboxMode.Parse(settings.Sandbox);

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK AppServer Demo", "1.0.0"));
        });

        try
        {
            await using var codex = await sdk.AppServer.StartAsync(ct);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                Cwd = repoPath,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox
            }, ct);

            await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text("Summarize this repo.")],
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
