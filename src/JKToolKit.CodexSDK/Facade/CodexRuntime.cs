using System.Diagnostics;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Abstractions;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>Inspects the selected runtime without starting a model turn.</summary>
public sealed class CodexRuntime
{
    private readonly CodexThreads _threads;
    private readonly CodexAppServerClientOptions? _options;
    private readonly ICodexPathProvider? _pathProvider;
    internal CodexRuntime(CodexThreads threads, CodexAppServerClientOptions? options, ICodexPathProvider? pathProvider)
    { _threads = threads; _options = options?.Clone(); _pathProvider = pathProvider; }

    /// <summary>Gets the protocol version embedded from UPSTREAM_CODEX_VERSION.json at build time.</summary>
    public static string ExpectedVersion { get; } = ReadExpectedVersion();

    /// <summary>Reads the protocol pin embedded into this SDK build.</summary>
    private static string ReadExpectedVersion()
    {
        using var stream = typeof(CodexRuntime).Assembly.GetManifestResourceStream("CodexUpstreamVersion")!;
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.GetProperty("api").GetString()!;
    }

    /// <summary>Probes the executable and optionally initializes the shared server to inspect metadata and account status.</summary>
    /// <remarks>Version differences are reported, not rejected. Each probe has a timeout. Caller cancellation propagates.</remarks>
    public async Task<CodexRuntimeInfo> GetInfoAsync(bool includeServer = true, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var diagnostics = new List<string>();
        string? path = null, version = null;
        AppServerInitializeResult? initialize = null;
        AccountReadResult? account = null;
        if (_options is not null && _pathProvider is not null && _options.Endpoint is null && string.IsNullOrWhiteSpace(_options.Launch.FileName))
        {
            try
            {
                path = _pathProvider.GetCodexExecutablePath(_options.CodexExecutablePath);
                version = await ReadVersionAsync(path, ct).ConfigureAwait(false);
                if (version != ExpectedVersion)
                    diagnostics.Add($"SDK generated for {ExpectedVersion}, found CLI {version}.");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            { diagnostics.Add($"Executable preflight failed: {ex.GetType().Name}: {ex.Message}"); }
        }
        else diagnostics.Add("Executable preflight is unavailable for a remote endpoint or custom client/launch; consult initialize metadata.");

        if (includeServer)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var client = await _threads.GetClientAsync(timeout.Token).ConfigureAwait(false);
                initialize = client.InitializeResult;
                account = await client.ReadAccountAsync(new AccountReadOptions { RefreshToken = false }, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            { diagnostics.Add($"Server preflight failed: {ex.GetType().Name}: {ex.Message}"); }
        }
        JsonElement? capabilities = initialize?.Raw is { ValueKind: JsonValueKind.Object } raw &&
            raw.TryGetProperty("capabilities", out var value) ? value.Clone() : null;
        return new CodexRuntimeInfo
        {
            ExecutablePath = path, ActualVersion = version, ExpectedVersion = ExpectedVersion,
            Initialize = initialize, Account = account, Capabilities = capabilities,
            Diagnostics = diagnostics.AsReadOnly()
        };
    }

    /// <summary>Builds an executable or Windows batch-shim version probe with redirected output.</summary>
    internal static ProcessStartInfo CreateVersionStartInfo(string path, bool isWindows)
    {
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        var extension = Path.GetExtension(path);
        if (isWindows && (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            if (path.IndexOfAny(['"', '\r', '\n', '\0']) >= 0)
                throw new ArgumentException("Invalid Windows batch executable path.", nameof(path));
            info.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            // Expand the path once inside quotes, keeping literal percent signs intact. Disable
            // AutoRun and delayed expansion so a shim path containing !, &, or spaces stays literal.
            info.Environment["CODEX_SDK_PREFLIGHT_BINARY"] = path;
            info.Arguments = "/d /v:off /s /c \"\"%CODEX_SDK_PREFLIGHT_BINARY%\" --version\"";
        }
        else info.ArgumentList.Add("--version");
        return info;
    }

    /// <summary>Collects a bounded version probe, terminating its process tree on timeout or cancellation.</summary>
    private static async Task<string> ReadVersionAsync(string path, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var info = CreateVersionStartInfo(path, OperatingSystem.IsWindows());
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Codex.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var text = (await output.ConfigureAwait(false)).Trim();
            await error.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException($"codex --version exited with code {process.ExitCode}.");
            const string prefix = "codex-cli ";
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Unrecognized codex --version output.");
            return text[prefix.Length..].Trim();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
