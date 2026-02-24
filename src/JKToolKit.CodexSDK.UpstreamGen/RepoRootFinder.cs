namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class RepoRootFinder
{
    public static string FindRepoRoot()
    {
        if (TryFindRepoRoot(Directory.GetCurrentDirectory(), out var root) ||
            TryFindRepoRoot(AppContext.BaseDirectory, out root))
        {
            return root;
        }

        throw new InvalidOperationException(
            "Could not locate repo root (JKToolKit.CodexSDK.sln) from current directory or AppContext.BaseDirectory.");
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

