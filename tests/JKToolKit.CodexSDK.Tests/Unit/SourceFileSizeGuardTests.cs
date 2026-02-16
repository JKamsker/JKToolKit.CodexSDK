using Xunit;
using Xunit.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SourceFileSizeGuardTests
{
    private readonly ITestOutputHelper _output;

    public SourceFileSizeGuardTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    [Fact]
    public void NoSrcFileExceeds500Lines_AndWarnsAbove300()
    {
        var repoRoot = FindRepoRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        Assert.True(Directory.Exists(srcRoot), $"Expected directory to exist: {srcRoot}");

        var files = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Select(fullPath => (FullPath: fullPath, RelativePath: Path.GetRelativePath(repoRoot, fullPath)))
            .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var warnings = new List<(int Lines, string Path)>();
        var failures = new List<(int Lines, string Path)>();

        foreach (var file in files)
        {
            var lineCount = CountLines(file.FullPath);
            if (lineCount > 500)
            {
                failures.Add((lineCount, file.RelativePath));
            }
            else if (lineCount > 300)
            {
                warnings.Add((lineCount, file.RelativePath));
            }
        }

        foreach (var warn in warnings
                     .OrderByDescending(x => x.Lines)
                     .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            _output.WriteLine($"WARN >300 LOC: {warn.Lines} {warn.Path}");
        }

        if (failures.Count > 0)
        {
            var message = "Files exceed 500 lines:\n" + string.Join(
                "\n",
                failures
                    .OrderByDescending(x => x.Lines)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"{x.Lines} {x.Path}"));

            Assert.Fail(message);
        }
    }

    private static int CountLines(string path)
    {
        return File.ReadLines(path).Count();
    }

    private static string FindRepoRoot()
    {
        if (TryFindRepoRoot(Directory.GetCurrentDirectory(), out var root) ||
            TryFindRepoRoot(AppContext.BaseDirectory, out root))
        {
            return root;
        }

        throw new InvalidOperationException("Could not locate repo root (JKToolKit.CodexSDK.sln) from current directory or AppContext.BaseDirectory.");
    }

    private static bool TryFindRepoRoot(string startDirectory, out string root)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "JKToolKit.CodexSDK.sln")))
            {
                root = dir.FullName;
                return true;
            }
        }

        root = string.Empty;
        return false;
    }
}
