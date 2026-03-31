using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexExecParityDriftTests
{
    [Fact]
    public void CreateProcessStartInfo_DoesNotForceModelOrReasoningOverride_WhenNotExplicitlySet()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt");
            var launcher = CreateLauncher();

            var startInfo = launcher.CreateProcessStartInfo(options, new CodexClientOptions());

            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateResumeStartInfo_DoesNotForceModelOrReasoningOverride_WhenNotExplicitlySet()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "follow-up");
            var launcher = CreateLauncher();

            var startInfo = launcher.CreateResumeStartInfo(SessionId.Parse("session-abc"), options, new CodexClientOptions());

            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "resume",
                "session-abc",
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_EmitsModelAndReasoningOverride_WhenExplicitlySetToDefaults()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                Model = CodexModel.Default,
                ReasoningEffort = CodexReasoningEffort.Medium
            };
            var launcher = CreateLauncher();

            var startInfo = launcher.CreateProcessStartInfo(options, new CodexClientOptions());

            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "--model",
                "gpt-5.2",
                "--config",
                "model_reasoning_effort=medium",
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_AllowsEphemeralOption_ForCliParity()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                AdditionalOptions = new[] { "--ephemeral" }
            };
            var launcher = CreateLauncher();

            var startInfo = launcher.CreateProcessStartInfo(options, new CodexClientOptions());

            startInfo.ArgumentList.Should().Contain("--ephemeral");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateResumeStartInfo_AllowsEphemeralOption_ForCliParity()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                AdditionalOptions = new[] { "--ephemeral=true" }
            };
            var launcher = CreateLauncher();

            var startInfo = launcher.CreateResumeStartInfo(SessionId.Parse("session-abc"), options, new CodexClientOptions());

            startInfo.ArgumentList.Should().Contain("--ephemeral=true");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void SessionOptions_ResumePayload_IgnoresStdinPayload_InPromptArgumentMode()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions
            {
                WorkingDirectory = workingDirectory,
                PromptArgument = "Continue",
                StdinPayload = "stdin payload"
            };

            options.StandardInputPayload.Should().Be("stdin payload");
            options.ResumeStandardInputPayload.Should().BeNull();
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Clone_PreservesExplicitOverrideFlags()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var defaults = new CodexSessionOptions(workingDirectory, "prompt");
            var defaultsClone = defaults.Clone();
            defaultsClone.HasExplicitModelOverride.Should().BeFalse();
            defaultsClone.HasExplicitReasoningEffortOverride.Should().BeFalse();

            var explicitOverrides = new CodexSessionOptions(workingDirectory, "prompt")
            {
                Model = CodexModel.Default,
                ReasoningEffort = CodexReasoningEffort.Medium
            };
            var explicitClone = explicitOverrides.Clone();
            explicitClone.HasExplicitModelOverride.Should().BeTrue();
            explicitClone.HasExplicitReasoningEffortOverride.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static CodexProcessLauncher CreateLauncher() =>
        new(new RecordingPathProvider("codex-default"), NullLogger<CodexProcessLauncher>.Instance);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingPathProvider(string defaultPath) : ICodexPathProvider
    {
        public string GetCodexExecutablePath(string? overridePath) =>
            overridePath ?? defaultPath;

        public string GetSessionsRootDirectory(string? overrideDirectory) =>
            throw new NotImplementedException();

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) =>
            throw new NotImplementedException();
    }
}
