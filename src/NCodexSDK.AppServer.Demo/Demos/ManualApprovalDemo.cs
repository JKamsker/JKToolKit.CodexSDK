using System.Text.Json;
using NCodexSDK.AppServer;
using NCodexSDK.AppServer.Notifications;
using NCodexSDK.Public.Models;

namespace NCodexSDK.AppServer.Demo.Demos;

/// <summary>
/// Demonstrates a restrictive approval policy where we only approve a single, explicit action:
/// creating <c>test.txt</c>.
/// </summary>
public sealed class ManualApprovalDemo : IAppServerDemo
{
    public async Task RunAsync(string repoPath, CancellationToken ct)
    {
        _ = repoPath; // demo runs in an isolated temp directory
        var workDir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"ncodexsdk-appserver-approval-demo-{Guid.NewGuid():N}")).FullName;

        var handler = new AllowOnlyTestTxtHandler();

        await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("ncodexsdk-demo", "NCodexSDK AppServer Approval Demo", "1.0.0"),
            ApprovalHandler = handler
        }, ct);

        var thread = await codex.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt51Codex,
            Cwd = workDir,
            ApprovalPolicy = CodexApprovalPolicy.OnRequest,
            Sandbox = CodexSandboxMode.WorkspaceWrite
        }, ct);

        await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Input = [TurnInputItem.Text("Create a file named test.txt in the current working directory with the content: hello")]
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

        var testPath = Path.Combine(workDir, "test.txt");
        Console.WriteLine(File.Exists(testPath)
            ? $"\nCreated: {testPath}"
            : $"\nNot created: {testPath}");
    }

    private sealed class AllowOnlyTestTxtHandler : IAppServerApprovalHandler
    {
        private int _approvedOnce;

        public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
        {
            var paramsText = @params?.ToString() ?? string.Empty;

            var isTestTxt = paramsText.Contains("test.txt", StringComparison.OrdinalIgnoreCase);

            var approve =
                isTestTxt &&
                Interlocked.Exchange(ref _approvedOnce, 1) == 0;

            Console.Error.WriteLine($"[approval] method={method} approve={approve}");

            using var doc = JsonDocument.Parse(approve ? """{"approved":true}""" : """{"approved":false}""");
            return ValueTask.FromResult(doc.RootElement.Clone());
        }
    }
}
