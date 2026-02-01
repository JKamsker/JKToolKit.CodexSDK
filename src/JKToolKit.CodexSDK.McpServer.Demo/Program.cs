using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Public.Models;

var repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await using var codex = await CodexMcpServerClient.StartAsync(new CodexMcpServerClientOptions(), cts.Token);

var tools = await codex.ListToolsAsync(cts.Token);
Console.WriteLine("Tools:");
foreach (var tool in tools)
{
    Console.WriteLine($"- {tool.Name}");
}

var run = await codex.StartSessionAsync(new CodexMcpStartOptions
{
    Prompt = "Run tests and summarize failures.",
    Cwd = repoPath,
    Sandbox = CodexSandboxMode.WorkspaceWrite,
    ApprovalPolicy = CodexApprovalPolicy.Never
}, cts.Token);

Console.WriteLine(run.Text);

var followUp = await codex.ReplyAsync(run.ThreadId, "Now propose fixes.", cts.Token);
Console.WriteLine(followUp.Text);

