using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerSteerAndReviewE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_TurnSteer_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

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
                TurnInputItem.Text("Write a very long answer (at least 8000 characters). Do not stop early.")
            ]
        }, cts.Token);

        await foreach (var ev in turn.Events(cts.Token))
        {
            if (ev is AgentMessageDeltaNotification)
            {
                break;
            }
        }

        var steerTurnId = await turn.SteerAsync([TurnInputItem.Text("Stop now and reply only with: ok")], cts.Token);
        steerTurnId.Should().NotBeNullOrWhiteSpace();

        _ = await turn.Completion.WaitAsync(cts.Token);
    }

    [CodexE2EFact]
    public async Task AppServer_ReviewStart_Inline_And_Detached_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(240));

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexModel.Gpt52Codex
        }, cts.Token);

        var inline = await client.StartReviewAsync(new ReviewStartOptions
        {
            ThreadId = thread.Id,
            Delivery = ReviewDelivery.Inline,
            Target = new ReviewTarget.UncommittedChanges()
        }, cts.Token);

        _ = await inline.Turn.Completion.WaitAsync(cts.Token);

        var detached = await client.StartReviewAsync(new ReviewStartOptions
        {
            ThreadId = thread.Id,
            Delivery = ReviewDelivery.Detached,
            Target = new ReviewTarget.UncommittedChanges()
        }, cts.Token);

        detached.ReviewThreadId.Should().NotBeNullOrWhiteSpace();
        _ = await detached.Turn.Completion.WaitAsync(cts.Token);
    }
}

