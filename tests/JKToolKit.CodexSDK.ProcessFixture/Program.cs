namespace JKToolKit.CodexSDK.ProcessFixture;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Match the SDK's redirected UTF-8 streams on every host code page.
        Console.InputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var mode = Environment.GetEnvironmentVariable("CODEX_TEST_MODE")
            ?? args.FirstOrDefault(arg => arg.StartsWith("--fixture-", StringComparison.Ordinal))?[10..]
            ?? args.FirstOrDefault() ?? "echo";
        var marker = Environment.GetEnvironmentVariable("CODEX_TEST_MARKER");
        if (marker is not null)
        {
            await File.WriteAllTextAsync(marker + ".tmp", Environment.ProcessId.ToString());
            File.Move(marker + ".tmp", marker, overwrite: true);
        }
        switch (mode)
        {
            case "appserver":
                using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    while (await Console.In.ReadLineAsync(deadline.Token) is { } line)
                    {
                        using var message = System.Text.Json.JsonDocument.Parse(line);
                        if (message.RootElement.TryGetProperty("id", out var id))
                            Console.Out.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                            {
                                id,
                                result = new { userAgent = "process-fixture", codexHome = Environment.GetEnvironmentVariable("CODEX_HOME") }
                            }));
                    }
                }
                return 0;
            case "ready-echo":
                Console.Out.WriteLine("ready");
                Console.Out.Write(await Console.In.ReadToEndAsync());
                return 0;
            case "wait":
                await Task.Delay(TimeSpan.FromSeconds(30));
                return 0;
            case "exit":
                return 23;
            case "output":
                Console.Write($"{Environment.GetEnvironmentVariable("CODEX_TEST_VALUE")}|{Environment.CurrentDirectory}|α");
                Console.Error.Write("stderr-β");
                return 7;
            case "stderr-boundary":
                Console.Error.WriteLine();
                Console.Error.WriteLine(new string('x', 4096));
                Console.Out.WriteLine("ready");
                await Console.In.ReadToEndAsync();
                return 0;
            case "tree":
                var childInfo = new System.Diagnostics.ProcessStartInfo("dotnet");
                childInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
                childInfo.ArgumentList.Add("wait");
                childInfo.Environment["CODEX_TEST_MARKER"] = Environment.GetEnvironmentVariable("CODEX_TEST_CHILD_MARKER")!;
                using (var child = System.Diagnostics.Process.Start(childInfo)!)
                    await child.WaitForExitAsync();
                return 0;
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
