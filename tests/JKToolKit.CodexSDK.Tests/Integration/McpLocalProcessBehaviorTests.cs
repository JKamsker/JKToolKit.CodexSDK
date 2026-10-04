using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.DependencyInjection;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class McpLocalProcessBehaviorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Start_HandshakesAndPassesHomeThroughRealStdio(bool useFactory, bool overrideHome)
    {
        // This is a local JSON-RPC peer, with no Codex executable, credentials, or network dependency.
        var launch = OperatingSystem.IsWindows()
            ? CodexLaunch.FromFileName("powershell.exe").WithArgs("-NoProfile", "-Command", """
                while ($null -ne ($line = [Console]::ReadLine())) {
                  $request = $line | ConvertFrom-Json
                  if ($null -ne $request.id) {
                    if ($request.jsonrpc -ne '2.0') { throw 'Missing JSON-RPC version' }
                    @{ jsonrpc = '2.0'; id = $request.id; result = @{ home = $env:CODEX_HOME } } | ConvertTo-Json -Compress -Depth 5 | ForEach-Object { [Console]::WriteLine($_) }
                  }
                }
                """)
            : CodexLaunch.FromFileName("/bin/sh").WithArgs("-c", """
                while IFS= read -r line; do
                  case "$line" in
                    *'"id":'*)
                      case "$line" in *'"jsonrpc":"2.0"'*) ;; *) exit 42 ;; esac
                      id=$(printf '%s' "$line" | sed -n 's/.*"id":\([0-9][0-9]*\).*/\1/p')
                      printf '{"jsonrpc":"2.0","id":%s,"result":{"home":"%s"}}\n' "$id" "$CODEX_HOME"
                      ;;
                  esac
                done
                """);
        launch = launch.WithEnvironment("CODEX_HOME", "launch-home");
        var options = new CodexMcpServerClientOptions
        {
            Launch = launch,
            CodexHomeDirectory = overrideHome ? "override-home" : null,
            StartupTimeout = TimeSpan.FromSeconds(15),
            ShutdownTimeout = TimeSpan.FromMilliseconds(250)
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var services = new ServiceCollection().AddLogging().AddCodexMcpServerClient(configure =>
        {
            configure.Launch = options.Launch;
            configure.CodexHomeDirectory = options.CodexHomeDirectory;
            configure.StartupTimeout = options.StartupTimeout;
            configure.ShutdownTimeout = options.ShutdownTimeout;
        }).BuildServiceProvider();
        await using var client = useFactory
            ? await services.GetRequiredService<ICodexMcpServerClientFactory>().StartAsync(deadline.Token)
            : await CodexMcpServerClient.StartAsync(options, deadline.Token);
        var response = await client.CallAsync("echo-home", null, deadline.Token);
        Assert.Equal(overrideHome ? "override-home" : "launch-home", response.GetProperty("home").GetString());
        Assert.Equal("launch-home", launch.Environment["CODEX_HOME"]);
    }
}
