using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerNotifications;

public sealed class AppServerNotificationsCommand : AsyncCommand<AppServerNotificationsSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerNotificationsSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);

            var model = string.IsNullOrWhiteSpace(settings.Model)
                ? CodexModel.Gpt52Codex
                : CodexModel.Parse(settings.Model);

            var approvalPolicy = string.IsNullOrWhiteSpace(settings.ApprovalPolicy)
                ? CodexApprovalPolicy.Never
                : CodexApprovalPolicy.Parse(settings.ApprovalPolicy);

            var sandbox = string.IsNullOrWhiteSpace(settings.Sandbox)
                ? CodexSandboxMode.WorkspaceWrite
                : CodexSandboxMode.Parse(settings.Sandbox);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                Cwd = repoPath,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox
            }, ct);

            Console.WriteLine($"Thread: {thread.Id}");

            using var notificationsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var typedCount = 0;
            var rawCount = 0;

            var typedTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var n in codex.Notifications(notificationsCts.Token))
                    {
                        typedCount++;
                        if (typedCount <= settings.PrintLimit)
                        {
                            Console.Error.WriteLine($"[typed] {n.Method}");
                        }
                    }
                }
                catch (OperationCanceledException) when (notificationsCts.IsCancellationRequested)
                {
                    // ignore
                }
            }, CancellationToken.None);

            var rawTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var n in codex.NotificationsRaw(notificationsCts.Token))
                    {
                        rawCount++;
                        if (rawCount <= settings.PrintLimit)
                        {
                            Console.Error.WriteLine($"[raw]   {n.Method}");
                        }
                    }
                }
                catch (OperationCanceledException) when (notificationsCts.IsCancellationRequested)
                {
                    // ignore
                }
            }, CancellationToken.None);

            Console.WriteLine($"> {settings.Prompt}");
            Console.WriteLine();

            await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text(settings.Prompt)]
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

            notificationsCts.Cancel();
            try { await typedTask.ConfigureAwait(false); } catch { /* ignore */ }
            try { await rawTask.ConfigureAwait(false); } catch { /* ignore */ }

            Console.WriteLine($"Global notifications observed: typed={typedCount}, raw={rawCount}");
            return 0;
        });
}
