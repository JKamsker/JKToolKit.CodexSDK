using NCodexSDK.AppServer;
using NCodexSDK.AppServer.Notifications;
using NCodexSDK.Public.Models;

namespace NCodexSDK.AppServer.Demo.Demos;

public sealed class StreamingDemo : IAppServerDemo
{
    public async Task RunAsync(string repoPath, CancellationToken ct)
    {
        await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("ncodexsdk-demo", "NCodexSDK AppServer Demo", "1.0.0"),
        }, ct);

        var thread = await codex.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt51Codex,
            Cwd = repoPath,
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.WorkspaceWrite
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
    }
}

