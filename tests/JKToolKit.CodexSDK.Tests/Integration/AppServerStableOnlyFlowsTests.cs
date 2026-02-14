using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
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
            Model = CodexModel.Gpt52Codex
        }, cts.Token);

        await using var turn = await client.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Input = [TurnInputItem.Text("Reply with 'ok'.")]
        }, cts.Token);

        _ = await turn.Completion.WaitAsync(cts.Token);
    }

    [CodexE2EFact]
    public async Task AppServer_StableOnly_ResumeThreadById_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexModel.Gpt52Codex
        }, cts.Token);

        var resumed = await client.ResumeThreadAsync(thread.Id, cts.Token);
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
            Model = CodexModel.Gpt52Codex
        }, cts.Token);

        await using var turn = await client.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Input =
            [
                TurnInputItem.Text("Write a very long answer (at least 8000 characters) so I can interrupt mid-stream. Start now.")
            ]
        }, cts.Token);

        var observedAny = false;
        await foreach (var ev in turn.Events(cts.Token))
        {
            observedAny = true;

            if (ev is AgentMessageDeltaNotification)
            {
                break;
            }
        }

        observedAny.Should().BeTrue("the stable path should stream at least one notification");

        await turn.InterruptAsync(cts.Token);

        var completed = await turn.Completion.WaitAsync(cts.Token);
        completed.Status.Should().NotBeNullOrWhiteSpace();
    }
}
