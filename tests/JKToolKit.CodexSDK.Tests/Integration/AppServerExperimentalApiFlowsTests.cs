using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
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

            await using var turn = await client.StartTurnAsync(threadId, new TurnStartOptions
            {
                Input = [TurnInputItem.Text("Reply only with: ok.")]
            }, cts.Token);

            _ = await turn.Completion.WaitAsync(cts.Token);

            rolloutPath = await WaitForRolloutPathAsync(threadId, cts.Token);
        }

        await using var client2 = await CodexAppServerClient.StartAsync(options, cts.Token);

        var resumed = await client2.ResumeThreadAsync(new ThreadResumeOptions
        {
            ThreadId = threadId,
            Path = rolloutPath
        }, cts.Token);

        resumed.Id.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<string> WaitForRolloutPathAsync(string threadId, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");

        while (!cts.IsCancellationRequested)
        {
            if (Directory.Exists(sessionsRoot))
            {
                var match = Directory
                    .EnumerateFiles(sessionsRoot, $"rollout-*{threadId}.jsonl", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException($"Timed out waiting for rollout path for thread '{threadId}'.");
    }
}
