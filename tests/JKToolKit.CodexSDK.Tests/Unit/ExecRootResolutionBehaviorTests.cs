using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.Tests.Unit;

[Collection("CurrentDirectory")]
public sealed class ExecRootResolutionBehaviorTests
{
    [Theory]
    [InlineData("sessions")]
    [InlineData("home")]
    [InlineData("environment")]
    [InlineData("provider")]
    public void RootResolution_PreservesPrecedenceAndCreatesOverrideAndEffectiveDirectories(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), "codex-root-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            var environment = Path.Combine(root, "environment");
            Environment.SetEnvironmentVariable("CODEX_HOME", source == "provider" ? null : environment);
            var options = new CodexClientOptions
            {
                SessionsRootDirectory = source == "sessions" ? Path.Combine(root, "explicit-sessions") : null,
                CodexHomeDirectory = source is "sessions" or "home" ? Path.Combine(root, "explicit-home") : null
            };
            var expectedOverride = source switch
            {
                "sessions" => options.SessionsRootDirectory,
                "home" => Path.Combine(options.CodexHomeDirectory!, "sessions"),
                "environment" => Path.Combine(environment, "sessions"),
                _ => null
            };
            var effective = Path.Combine(root, "provider-result");
            var provider = new PathProvider(effective);
            Assert.Equal(effective, CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(options, provider));
            Assert.Equal(expectedOverride, provider.Override);
            Assert.Equal(expectedOverride is not null, provider.OverrideExistedWhenCalled);
            Assert.True(Directory.Exists(effective));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ModelProvider_DoesNotInferConfigFromUnrelatedDirectoryName_AndUsesEnvironmentFallback()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "codex-config-" + Guid.NewGuid().ToString("N"))).FullName;
        var previous = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            var environment = Directory.CreateDirectory(Path.Combine(root, "environment")).FullName;
            File.WriteAllText(Path.Combine(environment, "config.toml"), "model_provider = \"environment\"");
            File.WriteAllText(Path.Combine(root, "config.toml"), "model_provider = \"wrong-parent\"");
            Environment.SetEnvironmentVariable("CODEX_HOME", environment);
            Assert.Equal("environment", CodexModelProviderConfigResolver.ResolveActiveModelProvider(new(), Path.Combine(root, "unrelated")));
            Assert.Equal("environment", CodexModelProviderConfigResolver.ResolveActiveModelProvider(new() { CodexHomeDirectory = Path.Combine(root, "missing") }, " "));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", previous);
            Directory.Delete(root, true);
        }
    }

    private sealed class PathProvider(string effective) : ICodexPathProvider
    {
        public string? Override;
        public bool OverrideExistedWhenCalled;
        public string GetSessionsRootDirectory(string? value)
        {
            Override = value;
            OverrideExistedWhenCalled = Directory.Exists(value);
            return effective;
        }
        public string GetCodexExecutablePath(string? value) => throw new NotSupportedException();
        public string ResolveSessionLogPath(SessionId id, string? root) => throw new NotSupportedException();
    }
}
