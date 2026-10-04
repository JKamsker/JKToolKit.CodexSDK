using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerStableOnlyFlowsTests
{
    [CodexE2EFact]
    public async Task AppServer_StableOnly_StartThread_AndStartTurn_Completes()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexLiveTestSettings.Model
        }, cts.Token);

        await using var turn = await client.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Effort = CodexReasoningEffort.Low,
            Input = [TurnInputItem.Text("Reply with 'ok'.")]
        }, cts.Token);

        CodexLiveTestSettings.AssertCompleted(await turn.Completion.WaitAsync(cts.Token));
    }

    [CodexE2EFact]
    public async Task AppServer_StableOnly_ResumeThreadById_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var options = new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        };

        string threadId;

        await using (var client = await CodexAppServerClient.StartAsync(options, cts.Token))
        {
            var thread = await client.StartThreadAsync(new ThreadStartOptions
            {
                Cwd = Directory.GetCurrentDirectory(),
                Model = CodexLiveTestSettings.Model
            }, cts.Token);

            threadId = thread.Id;

            await using var turn = await client.StartTurnAsync(threadId, new TurnStartOptions
            {
                Effort = CodexReasoningEffort.Low,
                Input = [TurnInputItem.Text("Reply only with: ok.")]
            }, cts.Token);

            CodexLiveTestSettings.AssertCompleted(await turn.Completion.WaitAsync(cts.Token));
        }

        await using var client2 = await CodexAppServerClient.StartAsync(options, cts.Token);

        var resumed = await client2.ResumeThreadAsync(threadId, cts.Token);
        resumed.Id.Should().NotBeNullOrWhiteSpace();
    }

    [CodexE2EFact]
    public async Task AppServer_StableOnly_StartTurn_ObserveNotifications_ThenInterrupt()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexLiveTestSettings.Model
        }, cts.Token);

        await using var turn = await client.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Effort = CodexReasoningEffort.Low,
            Input =
            [
                TurnInputItem.Text("Count from 1 to 1000 in words so I can interrupt mid-stream. Do not use tools or edit files.")
            ]
        }, cts.Token);

        var observedAny = false;
        await foreach (var ev in turn.Events(cts.Token))
        {
            observedAny = true;

            if (ev.Method == "turn/started" || ev is AgentMessageDeltaNotification)
            {
                break;
            }
        }

        observedAny.Should().BeTrue("the stable path should stream at least one notification");

        try
        {
            await turn.InterruptAsync(cts.Token);
        }
        catch (JsonRpcRemoteException ex) when (ex.Error.Code == -32600 &&
            ex.Error.Message == "no active turn to interrupt")
        {
            // A completed successful response may beat the interrupt RPC; a failed turn must not pass.
            CodexLiveTestSettings.AssertCompleted(await turn.Completion.WaitAsync(cts.Token));
            return;
        }

        var completed = await turn.Completion.WaitAsync(cts.Token);
        completed.Status.Should().BeOneOf("interrupted", "completed");
        completed.Error.Should().BeNull();
    }
}
