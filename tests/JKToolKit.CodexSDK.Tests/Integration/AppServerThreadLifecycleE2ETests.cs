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

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
            Model = CodexModel.Gpt52Codex,
            Ephemeral = false
        }, cts.Token);

        var list = await client.ListThreadsAsync(new ThreadListOptions
        {
            PageSize = 50
        }, cts.Token);

        list.Threads.Select(t => t.ThreadId).Should().Contain(thread.Id);

        var read = await client.ReadThreadAsync(thread.Id, cts.Token);
        read.Thread.ThreadId.Should().Be(thread.Id);

        _ = await client.ArchiveThreadAsync(thread.Id, cts.Token);

        var archivedList = await client.ListThreadsAsync(new ThreadListOptions
        {
            Archived = true,
            PageSize = 50
        }, cts.Token);

        archivedList.Threads.Select(t => t.ThreadId).Should().Contain(thread.Id);

        _ = await client.UnarchiveThreadAsync(thread.Id, cts.Token);

        var unarchivedList = await client.ListThreadsAsync(new ThreadListOptions
        {
            PageSize = 50
        }, cts.Token);

        unarchivedList.Threads.Select(t => t.ThreadId).Should().Contain(thread.Id);
    }
}
