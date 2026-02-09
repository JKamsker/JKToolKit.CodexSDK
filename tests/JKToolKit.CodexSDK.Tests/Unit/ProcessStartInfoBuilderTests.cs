using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class ProcessStartInfoBuilderTests
{
    [Fact]
    public void CreateProcessStartInfo_BuildsExpectedArguments_WithAdditionalOptions()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                Model = CodexModel.Gpt51CodexMini,
                ReasoningEffort = CodexReasoningEffort.High,
                AdditionalOptions = new[] { "--cheap-model", "--flag" }
            };
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be("codex-default");
            startInfo.WorkingDirectory.Should().Be(workingDirectory);
            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "--model",
                "gpt-5.1-codex-mini",
                "--config",
                "model_reasoning_effort=high",
                "--cheap-model",
                "--flag",
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateResumeStartInfo_BuildsExpectedArguments()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "follow-up")
            {
                Model = CodexModel.Gpt52Codex,
                ReasoningEffort = CodexReasoningEffort.Medium
            };
            var sessionId = SessionId.Parse("session-abc");
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateResumeStartInfo(sessionId, options, clientOptions);

            startInfo.FileName.Should().Be("codex-default");
            startInfo.WorkingDirectory.Should().Be(workingDirectory);
            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "--model",
                "gpt-5.2-codex",
                "--config",
                "model_reasoning_effort=medium",
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
    public void CreateProcessStartInfo_SetsCodexHomeEnvironmentVariable_WhenConfigured()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt");
            var clientOptions = new CodexClientOptions { CodexHomeDirectory = @"C:\codex\home" };
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.Environment["CODEX_HOME"].Should().Be(clientOptions.CodexHomeDirectory);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateResumeStartInfo_SetsCodexHomeEnvironmentVariable_WhenConfigured()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "follow-up");
            var clientOptions = new CodexClientOptions { CodexHomeDirectory = @"C:\codex\home" };
            var sessionId = SessionId.Parse("session-abc");
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateResumeStartInfo(sessionId, options, clientOptions);

            startInfo.Environment["CODEX_HOME"].Should().Be(clientOptions.CodexHomeDirectory);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_PrefersSessionBinaryOverride_OverClientOption()
    {
        var workingDirectory = CreateTempDirectory();
        var sessionBinary = Path.Combine(workingDirectory, "codex-session.exe");
        var clientBinary = Path.Combine(workingDirectory, "codex-client.exe");
        File.WriteAllText(sessionBinary, string.Empty);
        File.WriteAllText(clientBinary, string.Empty);

        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                CodexBinaryPath = sessionBinary
            };

            var clientOptions = new CodexClientOptions
            {
                CodexExecutablePath = clientBinary
            };

            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be(sessionBinary);
            pathProvider.LastOverride.Should().Be(sessionBinary);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_UsesPlatformDefaultExecutable_WhenNoOverrides()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt");
            var clientOptions = new CodexClientOptions();
            var expectedDefault = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
            var pathProvider = new RecordingPathProvider(expectedDefault);
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be(expectedDefault);
            pathProvider.LastOverride.Should().BeNull();
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_AllowsXHighOnlyWithGpt51CodexMax()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                Model = CodexModel.Gpt51CodexMax,
                ReasoningEffort = CodexReasoningEffort.XHigh
            };
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.ArgumentList.Should().Contain("model_reasoning_effort=xhigh");
            startInfo.ArgumentList.Should().Contain("gpt-5.1-codex-max");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_Throws_WhenXHighUsedWithoutGpt51CodexMax()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                Model = CodexModel.Gpt52Codex,
                ReasoningEffort = CodexReasoningEffort.XHigh
            };
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var act = () => launcher.CreateProcessStartInfo(options, clientOptions);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*xhigh*only supported with model 'gpt-5.1-codex-max'*");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateReviewStartInfo_BuildsExpectedArguments_WithCommitAndPrompt()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory)
            {
                CommitSha = "9a8ff41389e6684f222fb982f50efc04b59e0d50",
                Title = "Optional title",
                Prompt = "Focus on correctness and security.",
                AdditionalOptions = new[] { "--enable", "experimental_feature" }
            };
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateReviewStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be("codex-default");
            startInfo.WorkingDirectory.Should().Be(workingDirectory);
            startInfo.ArgumentList.Should().Equal(
                "-C",
                workingDirectory,
                "review",
                "--commit",
                "9a8ff41389e6684f222fb982f50efc04b59e0d50",
                "--title",
                "Optional title",
                "--enable",
                "experimental_feature",
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateReviewStartInfo_SetsCodexHomeEnvironmentVariable_WhenConfigured()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory);
            var clientOptions = new CodexClientOptions { CodexHomeDirectory = @"C:\codex\home" };
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateReviewStartInfo(options, clientOptions);

            startInfo.Environment["CODEX_HOME"].Should().Be(clientOptions.CodexHomeDirectory);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateReviewStartInfo_BuildsExpectedArguments_WithUncommittedWithoutPrompt()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory)
            {
                Uncommitted = true
            };
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateReviewStartInfo(options, clientOptions);

            startInfo.ArgumentList.Should().Equal(
                "-C",
                workingDirectory,
                "review",
                "--uncommitted");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingPathProvider : ICodexPathProvider
    {
        private readonly string _defaultPath;

        public RecordingPathProvider(string defaultPath)
        {
            _defaultPath = defaultPath;
        }

        public string? LastOverride { get; private set; }

        public string GetCodexExecutablePath(string? overridePath)
        {
            LastOverride = overridePath;
            return overridePath ?? _defaultPath;
        }

        public string GetSessionsRootDirectory(string? overrideDirectory) =>
            throw new NotImplementedException();

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) =>
            throw new NotImplementedException();
    }
}
