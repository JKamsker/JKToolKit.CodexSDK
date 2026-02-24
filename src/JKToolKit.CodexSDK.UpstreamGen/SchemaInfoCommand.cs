using System.Text.Json;

namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class SchemaInfoCommand
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
            string? writePath = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg is "--schema")
                {
                    schemaPath = RequireValue(args, ref i, "--schema");
                    continue;
                }

                if (arg is "--write")
                {
                    writePath = RequireValue(args, ref i, "--write");
                    continue;
                }

                throw new ArgumentException($"Unknown option '{arg}'.");
            }

            var repoRoot = RepoRootFinder.FindRepoRoot();
            var resolvedSchemaPath = schemaPath ?? UpstreamSchemaDiscovery.GetDefaultSchemaPath(repoRoot);

            var metadata = UpstreamSchemaDiscovery.GetMetadata(repoRoot, resolvedSchemaPath);

            var json = JsonSerializer.Serialize(metadata, UpstreamSchemaDiscovery.JsonOptions);
            Console.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(writePath))
            {
                var dir = Path.GetDirectoryName(writePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(writePath, json);
            }

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
        Console.WriteLine("  JKToolKit.CodexSDK.UpstreamGen schema-info [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --schema <PATH>    Path to codex_app_server_protocol.schemas.json");
        Console.WriteLine("  --write <PATH>     Write metadata JSON to this path");
        Console.WriteLine();
    }
}

