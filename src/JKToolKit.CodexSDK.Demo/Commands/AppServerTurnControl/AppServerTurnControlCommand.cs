using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerTurnControl;

public sealed class AppServerTurnControlCommand : AsyncCommand<AppServerTurnControlSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerTurnControlSettings settings, CancellationToken cancellationToken) =>
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
            Console.WriteLine($"> {settings.Prompt}");
            Console.WriteLine();

            await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text(settings.Prompt)]
            }, ct);

            var steerTask = StartSteerTask(turn, settings, ct);
            var interruptTask = StartInterruptTask(turn, settings, ct);

            await foreach (var ev in turn.Events(ct))
            {
                if (ev is AgentMessageDeltaNotification delta)
                {
                    Console.Write(delta.Delta);
                }
            }

            var exitCode = 0;
            try
            {
                var completed = await turn.Completion;
                Console.WriteLine($"\nDone: {completed.Status}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nCompletion failed: {ex.Message}");
                exitCode = 1;
            }

            try { await steerTask.ConfigureAwait(false); } catch { /* ignore */ }
            try { await interruptTask.ConfigureAwait(false); } catch { /* ignore */ }

            return exitCode;
        });

    private static Task StartSteerTask(CodexTurnHandle turn, AppServerTurnControlSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Steer))
        {
            return Task.CompletedTask;
        }

        var delayMs = settings.SteerAfterMs <= 0 ? 1000 : settings.SteerAfterMs;

        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct);

                if (settings.UseRawSteer)
                {
                    var result = await turn.SteerRawAsync([TurnInputItem.Text(settings.Steer)], ct);
                    Console.Error.WriteLine($"[steer] ok (turnId={result.TurnId})");
                }
                else
                {
                    var turnId = await turn.SteerAsync([TurnInputItem.Text(settings.Steer)], ct);
                    Console.Error.WriteLine($"[steer] ok (turnId={turnId})");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[steer] failed: {ex.Message}");
            }
        }, CancellationToken.None);
    }

    private static Task StartInterruptTask(CodexTurnHandle turn, AppServerTurnControlSettings settings, CancellationToken ct)
    {
        if (settings.InterruptAfterMs <= 0)
        {
            return Task.CompletedTask;
        }

        var delayMs = settings.InterruptAfterMs;

        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct);
                await turn.InterruptAsync(ct);
                Console.Error.WriteLine("[interrupt] sent");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[interrupt] failed: {ex.Message}");
            }
        }, CancellationToken.None);
    }
}

public sealed class AppServerTurnControlSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--approval-policy <POLICY>")]
    public string? ApprovalPolicy { get; init; }

    [CommandOption("--sandbox <MODE>")]
    public string? Sandbox { get; init; }

    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } =
        "Start listing the numbers from 1 upward, one per line. Keep going until interrupted.";

    [CommandOption("--steer <TEXT>")]
    public string? Steer { get; init; } = "Now stop and summarize in one sentence.";

    [CommandOption("--steer-after-ms <MS>")]
    public int SteerAfterMs { get; init; } = 1500;

    [CommandOption("--raw-steer")]
    public bool UseRawSteer { get; init; }

    [CommandOption("--interrupt-after-ms <MS>")]
    public int InterruptAfterMs { get; init; } = 3500;
}
