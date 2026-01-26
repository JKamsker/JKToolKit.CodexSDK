using NCodexSDK.AppServer;
using NCodexSDK.AppServer.Notifications;
using NCodexSDK.Public.Models;

var repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
{
    DefaultClientInfo = new("ncodexsdk-demo", "NCodexSDK AppServer Demo", "1.0.0"),
}, cts.Token);

var thread = await codex.StartThreadAsync(new ThreadStartOptions
{
    Model = CodexModel.Gpt51Codex,
    Cwd = repoPath,
    ApprovalPolicy = CodexApprovalPolicy.Never,
    Sandbox = CodexSandboxMode.WorkspaceWrite
}, cts.Token);

await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
{
    Input = [TurnInputItem.Text("Summarize this repo.")],
}, cts.Token);

await foreach (var ev in turn.Events(cts.Token))
{
    if (ev is AgentMessageDeltaNotification delta)
    {
        Console.Write(delta.Delta);
    }
}

var completed = await turn.Completion;
Console.WriteLine($"\nDone: {completed.Status}");

