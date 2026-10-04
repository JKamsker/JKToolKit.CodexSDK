using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.StructuredOutputs;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecStartupSchemaCleanupBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootResolutionFailure_DeletesMaterializedSchemaBeforeAnyProcessLaunch(bool resume)
    {
        var marker = Guid.NewGuid().ToString("N");
        var failure = new DirectoryNotFoundException("configured sessions root is unavailable");
        var paths = new FailingPaths(marker, failure);
        var launcher = new MockCodexProcessLauncher();
        using var client = new CodexClient(Options.Create(new CodexClientOptions()), launcher, pathProvider: paths);
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt")
        {
            OutputSchema = CodexOutputSchema.FromJson(JsonSerializer.SerializeToElement(new { type = "object", description = marker }))
        };
        try
        {
            var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => resume
                ? client.ResumeSessionAsync(SessionId.Parse("session"), options)
                : client.StartSessionAsync(options));
            Assert.Same(failure, exception);
            Assert.NotNull(paths.MaterializedPath);
            Assert.False(File.Exists(paths.MaterializedPath), "Startup setup failures must release the schema file created earlier in the same operation.");
            Assert.Empty(launcher.CapturedStarts);
        }
        finally
        {
            if (paths.MaterializedPath is not null) File.Delete(paths.MaterializedPath);
        }
    }

    private sealed class FailingPaths(string marker, Exception failure) : ICodexPathProvider
    {
        public string? MaterializedPath;
        public string GetSessionsRootDirectory(string? directory)
        {
            foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "codex-output-schema-*.json"))
            {
                try
                {
                    if (File.ReadAllText(file).Contains(marker, StringComparison.Ordinal))
                    {
                        MaterializedPath = file;
                        break;
                    }
                }
                catch (IOException) { /* Other concurrently running tests can delete their own temporary schemas. */ }
            }
            throw failure;
        }
        public string GetCodexExecutablePath(string? path) => throw new NotSupportedException();
        public string ResolveSessionLogPath(SessionId id, string? root) => throw new NotSupportedException();
    }
}
