namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class CheckCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help"))
            {
                PrintHelp();
                return 0;
            }

            string? schemaPath = null;
            string? outDir = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg is "--schema")
                {
                    schemaPath = RequireValue(args, ref i, "--schema");
                    continue;
                }

                if (arg is "--out")
                {
                    outDir = RequireValue(args, ref i, "--out");
                    continue;
                }

                throw new ArgumentException($"Unknown option '{arg}'.");
            }

            var repoRoot = RepoRootFinder.FindRepoRoot();
            schemaPath ??= UpstreamSchemaDiscovery.GetDefaultSchemaPath(repoRoot);
            outDir ??= Path.Combine(repoRoot, "src", "JKToolKit.CodexSDK", "Generated", "Upstream");

            if (!Directory.Exists(outDir))
            {
                throw new DirectoryNotFoundException($"Output directory not found: {outDir}");
            }

            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "JKToolKit.CodexSDK.UpstreamGen",
                "check",
                Guid.NewGuid().ToString("n"));

            Directory.CreateDirectory(tempRoot);

            try
            {
                AppServerV2DtoGenerator.Generate(repoRoot, schemaPath, tempRoot);

                var diff = CompareDirectories(expectedDir: outDir, actualDir: tempRoot);
                if (diff.HasDifferences)
                {
                    Console.Error.WriteLine("Generated output is stale.");
                    Console.Error.WriteLine($"Expected: {outDir}");
                    Console.Error.WriteLine();
                    WriteDiffSummary(diff);
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Fix by running:");
                    Console.Error.WriteLine("  JKToolKit.CodexSDK.UpstreamGen generate");
                    return 1;
                }

                Console.WriteLine("Generated output is up-to-date.");
                return 0;
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static DirectoryDiff CompareDirectories(string expectedDir, string actualDir)
    {
        var expectedFiles = EnumerateRelativeFiles(expectedDir);
        var actualFiles = EnumerateRelativeFiles(actualDir);

        var missing = actualFiles.Except(expectedFiles, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var extra = expectedFiles.Except(actualFiles, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        var common = expectedFiles.Intersect(actualFiles, StringComparer.Ordinal).ToArray();
        var changed = new List<string>();
        foreach (var relPath in common)
        {
            var expectedPath = Path.Combine(expectedDir, relPath);
            var actualPath = Path.Combine(actualDir, relPath);

            var expectedText = NormalizeText(File.ReadAllText(expectedPath));
            var actualText = NormalizeText(File.ReadAllText(actualPath));

            if (!string.Equals(expectedText, actualText, StringComparison.Ordinal))
            {
                changed.Add(relPath);
            }
        }

        changed.Sort(StringComparer.Ordinal);

        return new DirectoryDiff(missing, extra, changed);
    }

    private static HashSet<string> EnumerateRelativeFiles(string rootDir)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(rootDir, path);
            // Normalize to OS separator so Path.Combine works later.
            rel = rel.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            set.Add(rel);
        }

        return set;
    }

    private static string NormalizeText(string s) =>
        s.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void WriteDiffSummary(DirectoryDiff diff)
    {
        WriteList("Missing files", diff.Missing, max: 25);
        WriteList("Extra files", diff.Extra, max: 25);
        WriteList("Changed files", diff.Changed, max: 25);
    }

    private static void WriteList(string title, IReadOnlyList<string> items, int max)
    {
        if (items.Count == 0)
        {
            return;
        }

        Console.Error.WriteLine($"{title} ({items.Count}):");
        for (var i = 0; i < items.Count && i < max; i++)
        {
            Console.Error.WriteLine($"  - {items[i]}");
        }

        if (items.Count > max)
        {
            Console.Error.WriteLine($"  - ... ({items.Count - max} more)");
        }
    }

    private sealed record class DirectoryDiff(
        IReadOnlyList<string> Missing,
        IReadOnlyList<string> Extra,
        IReadOnlyList<string> Changed)
    {
        public bool HasDifferences => Missing.Count > 0 || Extra.Count > 0 || Changed.Count > 0;
    }

    private static string RequireValue(string[] args, ref int i, string optionName)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{optionName}'.");
        }

        i++;
        return args[i];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  JKToolKit.CodexSDK.UpstreamGen check [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --schema <PATH>    Path to codex_app_server_protocol.schemas.json");
        Console.WriteLine("  --out <DIR>        Output directory root (default: src/JKToolKit.CodexSDK/Generated/Upstream)");
        Console.WriteLine();
    }
}

