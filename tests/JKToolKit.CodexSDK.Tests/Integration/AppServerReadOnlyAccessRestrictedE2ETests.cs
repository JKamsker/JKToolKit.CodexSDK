using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Xunit.Abstractions;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerReadOnlyAccessRestrictedE2ETests(ITestOutputHelper output)
{
    // Verified against this pin; this does not claim the field was removed in this release.
    private static readonly Version ReadOnlyAccessKnownRemovedVersion = new(0, 160, 0);

    [CodexE2EFact]
    public async Task AppServer_ReadOnlyAccessRestricted_ValidatesSupportOrExactRemovalDiagnostic()
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

            var codexBuildVersion = client.InitializeResult?.CodexBuildVersion;

            var thread = await client.StartThreadAsync(new ThreadStartOptions
            {
                Cwd = tmpDir,
                Model = CodexLiveTestSettings.Model
            }, cts.Token);

            var options = new TurnStartOptions
            {
                Effort = CodexReasoningEffort.Low,
                SandboxPolicy = CodexSandboxPolicyBuilder.ReadOnlyRestricted([tmpDir], includePlatformDefaults: true),
                Input = [TurnInputItem.Text("Reply only with: ok.")]
            };

            codexBuildVersion.Should().NotBeNull("capability assertions require the CLI version");
            if (codexBuildVersion >= ReadOnlyAccessKnownRemovedVersion)
            {
                // Requiring rejection also detects accidental omission of the restriction on the wire.
                var error = await Assert.ThrowsAsync<JsonRpcRemoteException>(() =>
                    client.StartTurnAsync(thread.Id, options, cts.Token));
                AssertRemovalDiagnostic(error, codexBuildVersion);
                return;
            }

            try
            {
                await using var turn = await client.StartTurnAsync(thread.Id, options, cts.Token);
                CodexLiveTestSettings.AssertCompleted(await turn.Completion.WaitAsync(cts.Token));
                output.WriteLine($"CLI {codexBuildVersion}: restricted readOnly.access turn completed successfully.");
            }
            catch (JsonRpcRemoteException error)
            {
                // Earlier CLIs can also reject the legacy field; accept only the precise removal diagnostic.
                AssertRemovalDiagnostic(error, codexBuildVersion);
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

    private void AssertRemovalDiagnostic(JsonRpcRemoteException error, Version? version)
    {
        error.Error.Code.Should().Be(-32600);
        error.Error.Message.Should().Be(
            "Invalid request: readOnly.access is no longer supported; use permissionProfile for restricted reads");
        output.WriteLine($"CLI {version}: {error.Error.Message}");
    }

}
