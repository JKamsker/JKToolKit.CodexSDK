namespace JKToolKit.CodexSDK.ProcessFixture;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var mode = Environment.GetEnvironmentVariable("CODEX_TEST_MODE") ?? args.FirstOrDefault() ?? "echo";
        var marker = Environment.GetEnvironmentVariable("CODEX_TEST_MARKER");
        if (marker is not null) await File.WriteAllTextAsync(marker, Environment.ProcessId.ToString());
        switch (mode)
        {
            case "wait":
                await Task.Delay(TimeSpan.FromSeconds(30));
                return 0;
            case "exit":
                return 23;
            case "output":
                Console.Write($"{Environment.GetEnvironmentVariable("CODEX_TEST_VALUE")}|{Environment.CurrentDirectory}|α");
                Console.Error.Write("stderr-β");
                return 7;
            case "stderr":
                Console.Error.WriteLine();
                for (var i = 0; i < 205; i++) Console.Error.WriteLine($"line-{i}");
                Console.Error.WriteLine(new string('x', 5000));
                Console.Out.WriteLine("ready");
                await Console.In.ReadToEndAsync();
                return 0;
            default:
                Console.Out.Write(await Console.In.ReadToEndAsync());
                return 0;
        }
    }
}
