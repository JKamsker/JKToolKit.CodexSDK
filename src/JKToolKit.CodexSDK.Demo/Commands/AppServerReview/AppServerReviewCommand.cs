using System.Diagnostics;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerReview;

public sealed class AppServerReviewCommand : AsyncCommand<AppServerReviewSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerReviewSettings settings, CancellationToken cancellationToken) =>
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

            var target = ResolveTarget(settings, repoPath);
            var delivery = ResolveDelivery(settings.Delivery);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                Cwd = repoPath,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox
            }, ct);

            Console.WriteLine($"Thread: {thread.Id}");
            Console.WriteLine($"Target: {settings.Target}");
            Console.WriteLine($"Delivery: {(delivery?.ToString() ?? "default")}");
            Console.WriteLine();

            var review = await codex.StartReviewAsync(new ReviewStartOptions
            {
                ThreadId = thread.Id,
                Target = target,
                Delivery = delivery
            }, ct);

            Console.WriteLine($"Review turn: {review.Turn.TurnId} (thread={review.Turn.ThreadId})");
            if (!string.IsNullOrWhiteSpace(review.ReviewThreadId))
            {
                Console.WriteLine($"ReviewThreadId: {review.ReviewThreadId}");
            }
            Console.WriteLine();

            await using var turn = review.Turn;
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
        });

    private static ReviewTarget ResolveTarget(AppServerReviewSettings settings, string repoPath)
    {
        var target = (settings.Target ?? string.Empty).Trim().ToLowerInvariant();

        return target switch
        {
            "" or "uncommitted" => new ReviewTarget.UncommittedChanges(),
            "base" or "base-branch" => new ReviewTarget.BaseBranch(
                ResolveBaseBranch(repoPath, settings.BaseBranch)),
            "commit" => new ReviewTarget.Commit(
                sha: string.IsNullOrWhiteSpace(settings.CommitSha)
                    ? throw new InvalidOperationException("--commit-sha is required when --target=commit.")
                    : settings.CommitSha.Trim(),
                title: string.IsNullOrWhiteSpace(settings.CommitTitle) ? null : settings.CommitTitle.Trim()),
            "custom" => new ReviewTarget.Custom(
                string.IsNullOrWhiteSpace(settings.Instructions)
                    ? throw new InvalidOperationException("--instructions is required when --target=custom.")
                    : settings.Instructions.Trim()),
            _ => throw new InvalidOperationException($"Unknown --target '{settings.Target}'. Use: uncommitted|base|commit|custom.")
        };
    }

    private static string ResolveBaseBranch(string repoPath, string? baseBranch)
    {
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            return baseBranch.Trim();
        }

        var resolved =
            TryResolveRemoteDefaultBranch(repoPath, "origin") ??
            TryResolveRemoteDefaultBranch(repoPath, "upstream");

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        throw new InvalidOperationException("--base-branch is required when --target=base (unable to resolve repository default branch).");
    }

    private static string? TryResolveRemoteDefaultBranch(string repoPath, string remoteName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"symbolic-ref --quiet --short refs/remotes/{remoteName}/HEAD",
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

            if (!p.WaitForExit(2000) || p.ExitCode != 0)
            {
                return null;
            }

            var output = p.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var idx = output.LastIndexOf('/');
            if (idx >= 0 && idx < output.Length - 1)
            {
                return output[(idx + 1)..];
            }

            return output;
        }
        catch
        {
            return null;
        }
    }

    private static ReviewDelivery? ResolveDelivery(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "inline" => ReviewDelivery.Inline,
            "detached" => ReviewDelivery.Detached,
            _ => throw new InvalidOperationException($"Unknown --delivery '{raw}'. Use: inline|detached.")
        };
    }
}

public sealed class AppServerReviewSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--approval-policy <POLICY>")]
    public string? ApprovalPolicy { get; init; }

    [CommandOption("--sandbox <MODE>")]
    public string? Sandbox { get; init; }

    [CommandOption("--target <KIND>")]
    public string? Target { get; init; } = "uncommitted";

    [CommandOption("--delivery <MODE>")]
    public string? Delivery { get; init; } = "inline";

    [CommandOption("--base-branch <BRANCH>")]
    public string? BaseBranch { get; init; }

    [CommandOption("--commit-sha <SHA>")]
    public string? CommitSha { get; init; }

    [CommandOption("--commit-title <TEXT>")]
    public string? CommitTitle { get; init; }

    [CommandOption("--instructions <TEXT>")]
    public string? Instructions { get; init; }
}
