using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

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
}
