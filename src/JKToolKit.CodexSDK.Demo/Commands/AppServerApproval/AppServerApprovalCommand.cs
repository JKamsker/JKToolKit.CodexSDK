using System.Text.Json;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerApproval;

public sealed class AppServerApprovalCommand : AsyncCommand<AppServerApprovalSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerApprovalSettings settings, CancellationToken cancellationToken)
    {
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

        var workDir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"ncodexsdk-appserver-approval-demo-{Guid.NewGuid():N}")).FullName;

        var handler = new AllowOnlyTestTxtHandler();

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
            {
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK AppServer Approval Demo", "1.0.0");
                o.ApprovalHandler = handler;
            });
        });

        try
        {
            await using var codex = await sdk.AppServer.StartAsync(ct);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                Cwd = workDir,
                ApprovalPolicy = CodexApprovalPolicy.OnRequest,
                Sandbox = CodexSandboxMode.ReadOnly
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
