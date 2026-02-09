using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.McpServer.Internal;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// A client for interacting with the Codex CLI "mcp-server" JSON-RPC interface.
/// </summary>
public sealed class CodexMcpServerClient : IAsyncDisposable
{
    private readonly CodexMcpServerClientOptions _options;
    private readonly JsonRpcConnection _rpc;
    private readonly StdioProcess _process;
    private int _disposed;

    internal CodexMcpServerClient(
        CodexMcpServerClientOptions options,
        StdioProcess process,
        JsonRpcConnection rpc)
    {
        _options = options;
        _process = process;
        _rpc = rpc;

        _rpc.OnServerRequest = OnRpcServerRequestAsync;
    }

    /// <summary>
    /// Starts a new Codex MCP server process and returns a connected client.
    /// </summary>
    public static async Task<CodexMcpServerClient> StartAsync(
        CodexMcpServerClientOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loggerFactory = NullLoggerFactory.Instance;

        var stdioFactory = CodexJsonRpcBootstrap.CreateDefaultStdioFactory(loggerFactory);
        var launch = ApplyCodexHome(options.Launch, options.CodexHomeDirectory);
        var (process, rpc) = await CodexJsonRpcBootstrap.StartAsync(
            stdioFactory,
            loggerFactory,
            launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            options.NotificationBufferCapacity,
            options.SerializerOptionsOverride,
            includeJsonRpcHeader: true,
            ct);

        var client = new CodexMcpServerClient(options, process, rpc);
        await client.InitializeAsync(ct);
        return client;
    }

    private static CodexLaunch ApplyCodexHome(CodexLaunch launch, string? codexHomeDirectory)
    {
        if (string.IsNullOrWhiteSpace(codexHomeDirectory))
        {
            return launch;
        }

        return launch.WithEnvironment("CODEX_HOME", codexHomeDirectory);
    }

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the MCP server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _rpc.SendRequestAsync(method, @params, ct);

    /// <summary>
    /// Lists tools provided by the MCP server.
    /// </summary>
    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await _rpc.SendRequestAsync("tools/list", @params: null, ct);
        return McpToolsListParser.Parse(result);
    }

    /// <summary>
    /// Calls a tool by name with the provided arguments.
    /// </summary>
    public async Task<McpToolCallResult> CallToolAsync(string toolName, object arguments, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name cannot be empty or whitespace.", nameof(toolName));

        ArgumentNullException.ThrowIfNull(arguments);

        var result = await _rpc.SendRequestAsync(
            "tools/call",
            new { name = toolName, arguments },
            ct);

        return new McpToolCallResult(result);
    }

    /// <summary>
    /// Starts a new Codex session by invoking the MCP tool that wraps Codex.
    /// </summary>
    public async Task<CodexMcpSessionStartResult> StartSessionAsync(CodexMcpStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Prompt))
            throw new ArgumentException("Prompt is required.", nameof(options));

        var args = new Dictionary<string, object?>
        {
            ["prompt"] = options.Prompt,
            ["approval-policy"] = options.ApprovalPolicy?.ToMcpWireValue(),
            ["sandbox"] = options.Sandbox?.ToMcpWireValue(),
            ["cwd"] = options.Cwd,
            ["model"] = options.Model?.Value,
            ["include-plan-tool"] = options.IncludePlanTool
        };

        var call = await CallToolAsync("codex", args, ct);
        var parsed = CodexMcpResultParser.Parse(call.Raw);
        return new CodexMcpSessionStartResult(parsed.ThreadId, parsed.Text, parsed.StructuredContent, parsed.Raw);
    }

    /// <summary>
    /// Sends a reply prompt to an existing thread via the MCP tool.
    /// </summary>
    public async Task<CodexMcpReplyResult> ReplyAsync(string threadId, string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId is required.", nameof(threadId));
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is required.", nameof(prompt));

        var args = new Dictionary<string, object?>
        {
            ["threadId"] = threadId,
            ["prompt"] = prompt
        };

        var call = await CallToolAsync("codex-reply", args, ct);
        var parsed = CodexMcpResultParser.Parse(call.Raw);
        return new CodexMcpReplyResult(parsed.ThreadId, parsed.Text, parsed.StructuredContent, parsed.Raw);
    }

    internal async Task InitializeAsync(CancellationToken ct)
    {
        var clientInfo = _options.ClientInfo;
        await _rpc.SendRequestAsync(
            "initialize",
            new
            {
                protocolVersion = "2025-06-18",
                clientInfo = new { name = clientInfo.Name, title = clientInfo.Title, version = clientInfo.Version },
                capabilities = new { }
            },
            ct);

        await _rpc.SendNotificationAsync("notifications/initialized", @params: null, ct);
    }

    private async ValueTask<JsonRpcResponse> OnRpcServerRequestAsync(JsonRpcRequest req)
    {
        var handler = _options.ElicitationHandler;
        if (handler is null)
        {
            return new JsonRpcResponse(
                req.Id,
                Result: null,
                Error: new JsonRpcError(-32601, $"Unhandled server request '{req.Method}'."));
        }

        try
        {
            var result = await handler.HandleAsync(req.Method, req.Params, CancellationToken.None);
            return new JsonRpcResponse(req.Id, Result: result, Error: null);
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse(req.Id, Result: null, Error: new JsonRpcError(-32000, ex.Message));
        }
    }

    /// <summary>
    /// Disposes the underlying MCP server connection and terminates the process.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _rpc.DisposeAsync();
        await _process.DisposeAsync();
    }
}
