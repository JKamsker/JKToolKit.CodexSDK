using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerThreadLifecycleE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_ThreadLifecycle_StartListReadArchiveUnarchive()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var cwd = Directory.GetCurrentDirectory();
        string threadId;

        await using (var initialClient = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token))
        {
            var thread = await initialClient.StartThreadAsync(new ThreadStartOptions
            {
                Cwd = cwd,
                Model = CodexModel.Gpt52Codex,
                Ephemeral = false
            }, cts.Token);

            threadId = thread.Id;

            await using (var turn = await initialClient.StartTurnAsync(threadId, new TurnStartOptions
            {
                Input =
                [
                    TurnInputItem.Text("Reply only with: ok")
                ]
            }, cts.Token))
            {
                _ = await turn.Completion.WaitAsync(cts.Token);
            }

            var loadedList = await WaitForLoadedThreadAsync(initialClient, threadId, cts.Token);
            loadedList.ThreadIds.Should().Contain(threadId);
        }

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var list = await WaitForThreadListStateAsync(client, threadId, archived: false, shouldContain: true, cts.Token);
        list.Threads.Select(t => t.ThreadId).Should().Contain(threadId);

        var read = await client.ReadThreadAsync(threadId, cts.Token);
        read.Thread.ThreadId.Should().Be(threadId);

        _ = await client.ArchiveThreadAsync(threadId, cts.Token);

        var archivedList = await WaitForThreadListStateAsync(client, threadId, archived: true, shouldContain: true, cts.Token);
        await WaitForThreadListStateAsync(client, threadId, archived: false, shouldContain: false, cts.Token);

        archivedList.Threads.Select(t => t.ThreadId).Should().Contain(threadId);

        _ = await client.UnarchiveThreadAsync(threadId, cts.Token);

        var unarchivedList = await WaitForThreadListStateAsync(client, threadId, archived: false, shouldContain: true, cts.Token);
        await WaitForThreadListStateAsync(client, threadId, archived: true, shouldContain: false, cts.Token);

        unarchivedList.Threads.Select(t => t.ThreadId).Should().Contain(threadId);
    }

    private static async Task<CodexLoadedThreadListPage> WaitForLoadedThreadAsync(
        CodexAppServerClient client,
        string threadId,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));

        CodexLoadedThreadListPage? lastPage = null;

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                lastPage = await client.ListLoadedThreadsAsync(new ThreadLoadedListOptions
                {
                    Limit = 50
                }, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                break;
            }

            if (lastPage.ThreadIds.Contains(threadId, StringComparer.Ordinal))
            {
                return lastPage;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }
        }

        throw BuildTimeoutException(
            $"loaded thread list to contain '{threadId}'",
            lastPage?.ThreadIds ?? Array.Empty<string>());
    }

    private static async Task<CodexThreadListPage> WaitForThreadListStateAsync(
        CodexAppServerClient client,
        string threadId,
        bool archived,
        bool shouldContain,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        CodexThreadListPage? lastPage = null;

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                lastPage = await client.ListThreadsAsync(new ThreadListOptions
                {
                    Archived = archived,
                    Limit = 50,
                    SortKey = "updated_at"
                }, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                break;
            }

            var containsThread = lastPage.Threads
                .Select(t => t.ThreadId)
                .Contains(threadId, StringComparer.Ordinal);

            if (containsThread == shouldContain)
            {
                return lastPage;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }
        }

        throw BuildTimeoutException(
            $"thread/list archived={archived} to {(shouldContain ? "contain" : "exclude")} '{threadId}'",
            lastPage?.Threads.Select(t => t.ThreadId) ?? Array.Empty<string>());
    }

    private static TimeoutException BuildTimeoutException(string expectation, IEnumerable<string> threadIds)
    {
        var ids = string.Join(", ", threadIds);
        if (string.IsNullOrWhiteSpace(ids))
        {
            ids = "<empty>";
        }

        return new TimeoutException($"Timed out waiting for {expectation}. Last observed thread ids: {ids}");
    }
}
