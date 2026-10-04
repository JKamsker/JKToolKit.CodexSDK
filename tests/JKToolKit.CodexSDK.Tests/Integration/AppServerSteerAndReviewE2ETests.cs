using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
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
            Model = CodexLiveTestSettings.Model
        }, cts.Token);

        await using var turn = await client.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Effort = CodexReasoningEffort.Low,
            Input =
            [
                TurnInputItem.Text("Write three sentences about testing. Do not use tools or edit files.")
            ]
        }, cts.Token);

        var steerTurnId = await turn.SteerAsync([TurnInputItem.Text("Stop now and reply only with: ok")], cts.Token);
        steerTurnId.Should().Be(turn.TurnId);

        CodexLiveTestSettings.AssertCompleted(await turn.Completion.WaitAsync(cts.Token));
    }

    [CodexE2EFact]
    public async Task AppServer_ReviewStart_Inline_Completes()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(240));

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexLiveTestSettings.Model,
            Config = System.Text.Json.JsonSerializer.SerializeToElement(new { model_reasoning_effort = "low" })
        }, cts.Token);

        var inline = await client.StartReviewAsync(new ReviewStartOptions
        {
            ThreadId = thread.Id,
            Delivery = ReviewDelivery.Inline,
            Target = new ReviewTarget.Custom("Review this self-contained change: a comment typo was corrected from teh to the. Do not use tools or edit files. Report no findings if correct.")
        }, cts.Token);

        await using var inlineTurn = inline.Turn;
        CodexLiveTestSettings.AssertCompleted(await inlineTurn.Completion.WaitAsync(cts.Token));
    }

    [CodexE2EFact]
    public async Task AppServer_DetachedReview_OnPaginatedThread_ReportsUnsupportedCapability()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions(), cts.Token);
        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexLiveTestSettings.Model,
            Ephemeral = false
        }, cts.Token);

        var error = await Assert.ThrowsAsync<CodexAppServerRequestFailedException>(() =>
            client.StartReviewAsync(new ReviewStartOptions
            {
                ThreadId = thread.Id,
                Delivery = ReviewDelivery.Detached,
                Target = new ReviewTarget.Custom("Review a comment typo correction. Do not use tools or edit files.")
            }, cts.Token));

        error.Method.Should().Be("review/start");
        error.ErrorCode.Should().Be(-32600);
        error.ErrorMessage.Should().Be("paginated threads do not support detached review");
    }
}

