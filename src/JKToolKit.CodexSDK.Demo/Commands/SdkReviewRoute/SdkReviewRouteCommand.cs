using System.Diagnostics;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.SdkReviewRoute;

public sealed class SdkReviewRouteCommand : AsyncCommand<SdkReviewRouteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SdkReviewRouteSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = settings.RepoPath ?? Directory.GetCurrentDirectory();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        try
        {
            Console.CancelKeyPress += cancelHandler;

            var headSha = TryGetGitHeadSha(repoPath);
            Console.WriteLine($"Repo: {repoPath}");
            Console.WriteLine($"HEAD: {(headSha ?? "n/a")}");
            Console.WriteLine();

            await using var sdk = CodexSdk.Create(builder =>
            {
                builder.CodexExecutablePath = settings.CodexExecutablePath;
                builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            });

            // 1) Exec review routing
            if (!string.IsNullOrWhiteSpace(headSha))
            {
                Console.WriteLine("Phase 1: CodexSdk.ReviewAsync (Exec)...");

                var exec = new CodexReviewOptions(repoPath)
                {
                    CommitSha = headSha,
                    Prompt = null
                };

                await using var routed = await sdk.ReviewAsync(new CodexSdkReviewOptions
                {
                    Mode = CodexSdkReviewMode.Exec,
                    Exec = exec
                }, ct);

                var result = routed.Exec ?? throw new InvalidOperationException("Expected Exec review result.");
                Console.WriteLine($"[exec] exit={result.ExitCode} session={result.SessionId?.Value ?? "n/a"}");
                if (result.ExitCode != 0)
                {
                    Console.Error.WriteLine(result.StandardError);
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("Phase 1: CodexSdk.ReviewAsync (Exec) skipped (git HEAD not available).");
            }

            Console.WriteLine();
            Console.WriteLine("Phase 2: CodexSdk.ReviewAsync (AppServer)...");

            var app = new CodexSdkAppServerReviewOptions
            {
                Thread = new ThreadStartOptions
                {
                    Model = CodexModel.Gpt52Codex,
                    Cwd = repoPath,
                    ApprovalPolicy = CodexApprovalPolicy.Never,
                    Sandbox = CodexSandboxMode.WorkspaceWrite
                },
                Target = new ReviewTarget.Custom("Say 'ok' and nothing else.")
            };

            await using (var routed = await sdk.ReviewAsync(new CodexSdkReviewOptions
            {
                Mode = CodexSdkReviewMode.AppServer,
                AppServer = app
            }, ct))
            {
                var session = routed.AppServer ?? throw new InvalidOperationException("Expected AppServer review session.");
                Console.WriteLine($"[app-server] thread={session.Thread.Id} reviewThread={session.Review.ReviewThreadId}");

                await foreach (var ev in session.Review.Turn.Events(ct))
                {
                    if (ev is AgentMessageDeltaNotification delta)
                    {
                        Console.Write(delta.Delta);
                    }
                }

                var completed = await session.Review.Turn.Completion;
                Console.WriteLine($"\nDone: {completed.Status}");
            }

            Console.WriteLine("ok");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled (timeout or Ctrl+C).");
            return 1;
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

    private static string? TryGetGitHeadSha(string repoPath)
    {
        // NOTE: Reading StandardOutput via ReadToEnd() after WaitForExit() can deadlock for large outputs.
        // This method is intentionally used only for small fixed-output commands (e.g., `git rev-parse HEAD`).
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            if (!p.WaitForExit(2000))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    _ = p.WaitForExit(2000); // best-effort drain; ignore whether it elapsed
                }
                catch
                {
                    // ignore
                }

                return null;
            }

            if (p.ExitCode != 0)
            {
                return null;
            }

            var sha = p.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(sha) ? null : sha;
        }
        catch
        {
            return null;
        }
    }
}
