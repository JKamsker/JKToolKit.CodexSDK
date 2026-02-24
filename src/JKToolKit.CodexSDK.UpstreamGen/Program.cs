using System.Reflection;

namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 1;
        }

        var cmd = args[0].Trim();

        return cmd switch
        {
            "schema-info" => SchemaInfoCommand.Run(args.Skip(1).ToArray()),
            "generate" => NotImplemented(cmd),
            "check" => NotImplemented(cmd),
            _ => UnknownCommand(cmd)
        };
    }

    private static bool IsHelp(string arg)
    {
        return string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static int NotImplemented(string command)
    {
        Console.Error.WriteLine($"Command '{command}' is not implemented yet.");
        return 2;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Console.Error.WriteLine();
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        Console.WriteLine($"JKToolKit.CodexSDK.UpstreamGen {version}");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  JKToolKit.CodexSDK.UpstreamGen <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  schema-info   Print upstream schema metadata (path/hash/version)");
        Console.WriteLine("  generate      Generate internal upstream wire DTOs");
        Console.WriteLine("  check         Verify generated output is up-to-date");
        Console.WriteLine();
        Console.WriteLine("schema-info options:");
        Console.WriteLine("  --schema <PATH>    Path to codex_app_server_protocol.schemas.json");
        Console.WriteLine("  --write <PATH>     Write metadata JSON to this path");
        Console.WriteLine();
    }
}
