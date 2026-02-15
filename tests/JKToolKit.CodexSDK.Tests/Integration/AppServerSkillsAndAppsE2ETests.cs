using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerSkillsAndAppsE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_ListSkills_And_ListApps_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
        }, cts.Token);

        CodexThread? thread = null;
        try
        {
            thread = await client.StartThreadAsync(new ThreadStartOptions
            {
                Cwd = Directory.GetCurrentDirectory(),
                Model = CodexModel.Gpt52Codex,
                Ephemeral = true
            }, cts.Token);

            var skills = await client.ListSkillsAsync(new SkillsListOptions
            {
                Cwd = Directory.GetCurrentDirectory()
            }, cts.Token);

            skills.Skills.Should().NotBeNull();

            var apps = await client.ListAppsAsync(new AppsListOptions
            {
                ThreadId = thread.Id
            }, cts.Token);

            apps.Apps.Should().NotBeNull();
        }
        finally
        {
            if (thread is not null)
            {
                try
                {
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    _ = await client.ArchiveThreadAsync(thread.Id, cleanupCts.Token);
                }
                catch
                {
                    // Best-effort cleanup for optional E2E tests.
                }
            }
        }
    }
}
