using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerSandboxPolicy;

public sealed class AppServerSandboxPolicyCommand : AsyncCommand<AppServerSandboxPolicySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerSandboxPolicySettings settings, CancellationToken cancellationToken)
    {
        var demoRoot = Path.Combine(Directory.GetCurrentDirectory(), ".tmp", "appserver-sandbox-policy", Guid.NewGuid().ToString("N"));

        try
        {
            return await AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
            {
                var allowedDir = Path.Combine(demoRoot, "allowed");
                var allowedFile = Path.Combine(allowedDir, "allowed.txt");

                var prompt = string.IsNullOrWhiteSpace(settings.Prompt)
                    ? "Create a file named allowed.txt in the current directory with content 'ok'. Finally, say 'done'."
                    : settings.Prompt;

                Directory.CreateDirectory(allowedDir);

                Console.WriteLine($"Demo root: {demoRoot}");
                Console.WriteLine($"Allowed dir: {allowedDir}");

                var thread = await codex.StartThreadAsync(new ThreadStartOptions
                {
                    Model = CodexModel.Gpt52Codex,
                    Cwd = allowedDir,
                    ApprovalPolicy = CodexApprovalPolicy.Never,
                    Sandbox = CodexSandboxMode.WorkspaceWrite
                }, ct);

                Console.WriteLine($"Thread: {thread.Id}");

                // Phase 1: Apply a read-only sandbox policy override and verify writes are blocked.
                Console.WriteLine("> Phase 1 (sandboxPolicy=readOnly):");
                Console.WriteLine($"> {prompt}");
                Console.WriteLine();

                await RunTurnAsync(codex, thread.Id, prompt, CodexSandboxPolicyBuilder.ReadOnly(), ct);

                var allowedExistsPhase1 = File.Exists(allowedFile);

                Console.WriteLine();
                Console.WriteLine($"allowed.txt exists (phase1): {allowedExistsPhase1}");

                if (allowedExistsPhase1)
                {
                    Console.Error.WriteLine("Expected allowed.txt to NOT be created under sandboxPolicy=readOnly.");
                    return 1;
                }

                // Phase 2: Apply a workspaceWrite sandbox policy override and verify writes succeed.
                Console.WriteLine();
                Console.WriteLine("> Phase 2 (sandboxPolicy=workspaceWrite):");
                Console.WriteLine($"> {prompt}");
                Console.WriteLine();

                var phase2Policy = CodexSandboxPolicyBuilder.WorkspaceWrite(
                    writableRoots: [allowedDir],
                    networkAccess: false,
                    excludeTmpdirEnvVar: true,
                    excludeSlashTmp: false,
                    readOnlyAccess: null);

                await RunTurnAsync(codex, thread.Id, prompt, phase2Policy, ct);

                var allowedExistsPhase2 = File.Exists(allowedFile);

                Console.WriteLine();
                Console.WriteLine($"allowed.txt exists (phase2): {allowedExistsPhase2}");

                if (!allowedExistsPhase2)
                {
                    Console.Error.WriteLine("Expected allowed.txt to be created under the writable root.");
                    return 1;
                }

                Console.WriteLine("ok");
                return 0;
            }).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(demoRoot);
        }
    }

    private static async Task RunTurnAsync(
        CodexAppServerClient codex,
        string threadId,
        string prompt,
        SandboxPolicy sandboxPolicy,
        CancellationToken ct)
    {
        await using var turn = await codex.StartTurnAsync(threadId, new TurnStartOptions
        {
            Input = [TurnInputItem.Text(prompt)],
            SandboxPolicy = sandboxPolicy
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Cleanup failed (file): {path}: {ex.Message}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Cleanup failed (dir): {path}: {ex.Message}");
        }
    }
}
