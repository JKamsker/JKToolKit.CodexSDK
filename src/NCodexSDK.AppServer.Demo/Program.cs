using NCodexSDK.AppServer.Demo.Demos;

namespace NCodexSDK.AppServer.Demo;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var demoName = GetArgValue(args, "--demo") ?? "stream";
        var repoPath = GetArgValue(args, "--repo") ?? GetFirstNonOptionArg(args) ?? Directory.GetCurrentDirectory();

        var timeoutSeconds = TryGetTimeoutSeconds(args) ??
            (int.TryParse(Environment.GetEnvironmentVariable("CODEX_DEMO_TIMEOUT_SECONDS"), out var envTimeout)
                ? envTimeout
                : (int?)null);

        using var cts = new CancellationTokenSource();
        if (timeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            IAppServerDemo demo = demoName.ToLowerInvariant() switch
            {
                "stream" or "basic" => new StreamingDemo(),
                "approve" or "approval" => new ManualApprovalDemo(),
                _ => throw new ArgumentException($"Unknown demo '{demoName}'. Use --demo stream|approve.")
            };

            await demo.RunAsync(repoPath, cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int? TryGetTimeoutSeconds(string[] args)
    {
        var value = GetArgValue(args, "--timeout-seconds");
        return int.TryParse(value, out var parsed) ? parsed : (int?)null;
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string? GetFirstNonOptionArg(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith("-", StringComparison.Ordinal))
            {
                return arg;
            }
        }

        return null;
    }
}
