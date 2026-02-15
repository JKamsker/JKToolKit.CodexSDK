using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerReadOnlyAccessRestrictedE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_ReadOnlyAccessRestricted_StartTurn_Succeeds_WhenSupported()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

        var tmpDir = Path.Combine(Path.GetTempPath(), "jktoolkit_codexsdk_roa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var filePath = Path.Combine(tmpDir, "hello.txt");
            await File.WriteAllTextAsync(filePath, "hello", cts.Token);

            await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
            {
                DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
            }, cts.Token);

            var thread = await client.StartThreadAsync(new ThreadStartOptions
            {
                Cwd = tmpDir,
                Model = CodexModel.Gpt52Codex
            }, cts.Token);

            try
            {
                await using var turn = await client.StartTurnAsync(thread.Id, new TurnStartOptions
                {
                    SandboxPolicy = CodexSandboxPolicyBuilder.ReadOnlyRestricted([tmpDir], includePlatformDefaults: true),
                    Input =
                    [
                        TurnInputItem.Text("Read the file 'hello.txt' in the current directory and reply with its contents only.")
                    ]
                }, cts.Token);

                var completed = await turn.Completion.WaitAsync(cts.Token);
                completed.Status.Should().NotBeNullOrWhiteSpace();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("rejected sandboxPolicy parameters", StringComparison.Ordinal))
            {
                // This is an optional E2E test, and older Codex app-server builds may not support ReadOnlyAccess overrides.
                return;
            }
        }
        finally
        {
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, recursive: true);
            }
        }
    }
}
