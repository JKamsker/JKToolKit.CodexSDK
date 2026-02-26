using FluentAssertions;
using JKToolKit.CodexSDK.Exec;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexReviewOptionsTests
{
    [Fact]
    public void Validate_Throws_WhenNoTargetProvided()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory);

            var act = () => options.Validate();

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Specify Uncommitted, BaseBranch, CommitSha, or provide Prompt.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Validate_AllowsPromptOnly()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory)
            {
                Prompt = "Focus on correctness and security."
            };

            options.Validate();
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Validate_Throws_WhenPromptIsCombinedWithCommit()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory)
            {
                CommitSha = "123456789",
                Prompt = "Focus on correctness and security."
            };

            var act = () => options.Validate();

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Prompt cannot be combined with CommitSha, BaseBranch, or Uncommitted.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Validate_Throws_WhenTitleIsUsedWithoutCommit()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory)
            {
                BaseBranch = "main",
                Title = "Optional title"
            };

            var act = () => options.Validate();

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Title requires CommitSha to be set.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Validate_Throws_WhenMultipleTargetsAreProvided()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexReviewOptions(workingDirectory)
            {
                BaseBranch = "main",
                Uncommitted = true
            };

            var act = () => options.Validate();

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Specify exactly one of Uncommitted, BaseBranch, CommitSha, or Prompt.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"codex-review-options-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

