using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

[Collection("CurrentDirectory")]
public sealed class PathAndLaunchGapCoverageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PathSearchSkipsFailedEntriesAndFindsExecutable(bool useExeFallback)
    {
        var first = Path.Combine(Path.GetTempPath(), "codex-denied");
        var second = Path.Combine(Path.GetTempPath(), "codex-found");
        var fileName = OperatingSystem.IsWindows() ? useExeFallback ? "codex.exe" : "codex.cmd" : "codex";
        var expected = Path.Combine(second, fileName);
        var files = new ExecutableFiles(path => path.StartsWith(first, StringComparison.Ordinal) ? throw new IOException("entry inaccessible") : path == expected);
        WithPath(first + Path.PathSeparator + second, () => Provider(files).GetCodexExecutablePath(null).Should().Be(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyPathReportsResolutionFailure(string? value)
    {
        WithPath(value, () => Assert.Throws<FileNotFoundException>(() => Provider(new ExecutableFiles(_ => false)).GetCodexExecutablePath(null))
            .Message.Should().Contain("PATH"));
    }

    [Fact]
    public void BundledRuntimeWinsBeforePathLookup()
    {
        var files = new ExecutableFiles(path => path.Contains("codex-runtime", StringComparison.Ordinal));
        var provider = Provider(files);
        var expected = provider.GetBundledExecutablePath(AppContext.BaseDirectory);
        if (expected is null) return; // Unsupported architectures intentionally have no bundled runtime.
        WithPath("", () => provider.GetCodexExecutablePath(null).Should().Be(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildersRejectEmptyExecutableBeforeBuildingArguments(string? executable)
    {
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt");
        Assert.Throws<ArgumentException>(() => ProcessStartInfoBuilder.Create(executable!, options)).ParamName.Should().Be("executablePath");
        Assert.Throws<ArgumentException>(() => ProcessStartInfoBuilder.CreateResume(executable!, CodexResumeTarget.BySelector("session"), options)).ParamName.Should().Be("executablePath");
        Assert.Throws<ArgumentException>(() => ProcessStartInfoBuilder.CreateReview(executable!, new() { WorkingDirectory = Path.GetTempPath(), Uncommitted = true })).ParamName.Should().Be("executablePath");
        Assert.Throws<ArgumentException>(() => StdioProcessStartInfoBuilder.Create(new() { ResolvedFileName = executable!, Arguments = [] }));
    }

    [Fact]
    public void ResumeIdOverloadHonorsExplicitTargetAndThreadSourceIsOnlyForNewSession()
    {
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt") { ThreadSource = "coverage-source", Images = ["first.png", "second.png"] };
        var launch = ProcessStartInfoBuilder.Create("codex", options);
        launch.ArgumentList.Should().ContainInOrder("--thread-source", "coverage-source");
        launch.ArgumentList.Should().ContainInOrder("--image", "first.png", "--image", "second.png");
        var resume = ProcessStartInfoBuilder.CreateResume("codex", SessionId.Parse("session"), options);
        resume.ArgumentList.Should().ContainInOrder("resume", "session", "-");
        resume.ArgumentList.Should().NotContain("--thread-source");
        options.ResumeTargetOverride = CodexResumeTarget.MostRecent();
        ProcessStartInfoBuilder.CreateResume("codex", SessionId.Parse("ignored"), options).ArgumentList.Should().ContainInOrder("resume", "--last", "-").And.NotContain("ignored");
    }

    [Fact]
    public void ReviewBaseBranchAndLegacyArgumentFormattingArePreserved()
    {
        var review = ProcessStartInfoBuilder.CreateReview("codex", new() { WorkingDirectory = Path.GetTempPath(), BaseBranch = "release branch" });
        review.ArgumentList.Should().ContainInOrder("review", "--base", "release branch");
        ProcessStartInfoBuilder.FormatArguments(review).Should().Contain("\"release branch\"");
        ProcessStartInfoBuilder.FormatArguments(new ProcessStartInfo { Arguments = "legacy --flag" }).Should().Be("legacy --flag");
        var escaped = new ProcessStartInfo();
        escaped.ArgumentList.Add("quoted\"value");
        ProcessStartInfoBuilder.FormatArguments(escaped).Should().Be("\"quoted\\\"value\"");
    }

    private static DefaultCodexPathProvider Provider(IFileSystem fs) => new(fs, NullLogger<DefaultCodexPathProvider>.Instance);
    private static void WithPath(string? path, Action action)
    {
        var original = Environment.GetEnvironmentVariable("PATH");
        try { Environment.SetEnvironmentVariable("PATH", path); action(); }
        finally { Environment.SetEnvironmentVariable("PATH", original); }
    }

    private sealed class ExecutableFiles(Func<string, bool> exists) : IFileSystem
    {
        public bool FileExists(string path) => exists(path);
        public bool DirectoryExists(string path) => true;
        public IEnumerable<string> GetFiles(string directory, string searchPattern) => [];
        public Stream OpenRead(string path) => throw new NotSupportedException();
        public DateTime GetFileCreationTimeUtc(string path) => throw new NotSupportedException();
        public long GetFileSize(string path) => 0;
    }
}
