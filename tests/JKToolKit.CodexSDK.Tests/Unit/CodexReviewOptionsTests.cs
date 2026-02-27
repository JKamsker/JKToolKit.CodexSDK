using FluentAssertions;
using JKToolKit.CodexSDK.Exec;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexReviewOptionsTests
{
    [Fact]
    public void Validate_Throws_WhenNoTargetProvided()
    {
        using var tmp = TempDirectory.Create();
        var options = new CodexReviewOptions(tmp.Path);

        var act = () => options.Validate();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Specify Uncommitted, BaseBranch, CommitSha, or provide Prompt.");
    }

    [Fact]
    public void Validate_AllowsPromptOnly()
    {
        using var tmp = TempDirectory.Create();
        var options = new CodexReviewOptions(tmp.Path)
        {
            Prompt = "Focus on correctness and security."
        };

        options.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenPromptIsCombinedWithCommit()
    {
        using var tmp = TempDirectory.Create();
        var options = new CodexReviewOptions(tmp.Path)
        {
            CommitSha = "123456789",
            Prompt = "Focus on correctness and security."
        };

        var act = () => options.Validate();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Prompt cannot be combined with CommitSha, BaseBranch, or Uncommitted.");
    }

    [Fact]
    public void Validate_Throws_WhenTitleIsUsedWithoutCommit()
    {
        using var tmp = TempDirectory.Create();
        var options = new CodexReviewOptions(tmp.Path)
        {
            BaseBranch = "main",
            Title = "Optional title"
        };

        var act = () => options.Validate();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Title requires CommitSha to be set.");
    }

    [Fact]
    public void Validate_Throws_WhenMultipleTargetsAreProvided()
    {
        using var tmp = TempDirectory.Create();
        var options = new CodexReviewOptions(tmp.Path)
        {
            BaseBranch = "main",
            Uncommitted = true
        };

        var act = () => options.Validate();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Specify exactly one of Uncommitted, BaseBranch, CommitSha, or Prompt.");
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codex-review-options-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
