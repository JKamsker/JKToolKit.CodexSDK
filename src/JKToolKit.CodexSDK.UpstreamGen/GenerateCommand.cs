namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class GenerateCommand
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

            AppServerV2DtoGenerator.Generate(repoRoot, schemaPath, outDir);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
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
        Console.WriteLine("  JKToolKit.CodexSDK.UpstreamGen generate [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --schema <PATH>    Path to codex_app_server_protocol.schemas.json");
        Console.WriteLine("  --out <DIR>        Output directory root (default: src/JKToolKit.CodexSDK/Generated/Upstream)");
        Console.WriteLine();
    }
}

