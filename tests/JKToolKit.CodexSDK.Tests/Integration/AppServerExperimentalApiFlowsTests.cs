using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerExperimentalApiFlowsTests
{
    [CodexE2EFact]
    public async Task AppServer_ExperimentalApi_ResumeThreadByPath_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var options = new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0"),
            ExperimentalApi = true
        };

        string threadId;
        string rolloutPath;

        await using (var client = await CodexAppServerClient.StartAsync(options, cts.Token))
        {
            var thread = await client.StartThreadAsync(new ThreadStartOptions
            {
                Cwd = Directory.GetCurrentDirectory(),
                Model = CodexModel.Gpt52Codex
            }, cts.Token);

            threadId = thread.Id;

            rolloutPath = await WaitForRolloutPathAsync(client, cts.Token);
        }

        await using var client2 = await CodexAppServerClient.StartAsync(options, cts.Token);

        var resumed = await client2.ResumeThreadAsync(new ThreadResumeOptions
        {
            ThreadId = threadId,
            Path = rolloutPath
        }, cts.Token);

        resumed.Id.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<string> WaitForRolloutPathAsync(CodexAppServerClient client, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        await foreach (var ev in client.Notifications(cts.Token))
        {
            if (ev is SessionConfiguredNotification configured &&
                !string.IsNullOrWhiteSpace(configured.RolloutPath))
            {
                return configured.RolloutPath!;
            }
        }

        throw new TimeoutException("Timed out waiting for SessionConfiguredNotification.rolloutPath.");
    }
}

