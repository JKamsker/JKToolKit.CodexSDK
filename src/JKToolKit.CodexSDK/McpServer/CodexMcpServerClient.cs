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
using JKToolKit.CodexSDK.McpServer.Overrides;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// A client for interacting with the Codex CLI "mcp-server" JSON-RPC interface.
/// </summary>
public sealed class CodexMcpServerClient : IAsyncDisposable
{
    private static readonly JsonElement DefaultElicitationDeniedResult = CreateDefaultElicitationDeniedResult();

    private readonly CodexMcpServerClientOptions _options;
    private readonly IJsonRpcConnection _rpc;
    private readonly IStdioProcess _process;
    private readonly ILogger<CodexMcpServerClient> _logger;
    private int _disposed;

    internal CodexMcpServerClient(
        CodexMcpServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger<CodexMcpServerClient> logger)
    {
        _options = options;
        _process = process;
        _rpc = rpc;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        var logger = NullLogger<CodexMcpServerClient>.Instance;

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

        return await CreateInitializedAsync(options, process, rpc, logger, ct).ConfigureAwait(false);
    }

    internal static async Task<CodexMcpServerClient> CreateInitializedAsync(
        CodexMcpServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger<CodexMcpServerClient> logger,
        CancellationToken ct)
    {
        var client = new CodexMcpServerClient(options, process, rpc, logger);

        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        handshakeCts.CancelAfter(options.StartupTimeout);

        try
        {
            await client.InitializeAsync(handshakeCts.Token).ConfigureAwait(false);
            return client;
        }
        catch
        {
            try { await client.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
            throw;
        }
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
    public async Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default)
    {
        var result = await _rpc.SendRequestAsync(method, @params, ct);
        return ApplyResponseTransformers(method, result);
    }

    /// <summary>
    /// Lists tools provided by the MCP server.
    /// </summary>
    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await _rpc.SendRequestAsync("tools/list", @params: null, ct);
        var transformed = ApplyResponseTransformers("tools/list", result);

        var customMappers = _options.ToolsListMappers;
        if (customMappers is { Count: > 0 })
        {
            foreach (var mapper in customMappers)
            {
                if (mapper is null)
                {
                    continue;
                }

                try
                {
                    var mapped = mapper.TryMap(transformed);
                    if (mapped is not null)
                    {
                        return mapped;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "MCP tools list mapper threw.");
                }
            }
        }

        const int maxPages = 100;
        var tools = new List<McpToolDescriptor>();
        string? cursor = null;

        for (var i = 0; i < maxPages; i++)
        {
            if (i != 0)
            {
                result = await _rpc.SendRequestAsync("tools/list", @params: new { cursor }, ct);
                transformed = ApplyResponseTransformers("tools/list", result);
            }

            if (!McpToolsListParser.TryParse(transformed, out var pageTools, out var nextCursor))
            {
                if (_options.StrictParsing)
                {
                    throw new JsonException("Unexpected tools/list result shape.");
                }

                _logger.LogWarning("Unexpected tools/list result shape: {Result}", Truncate(transformed.GetRawText(), maxChars: 4000));
                return tools;
            }

            tools.AddRange(pageTools);

            if (string.IsNullOrWhiteSpace(nextCursor))
            {
                break;
            }

            cursor = nextCursor;
        }

        return tools;
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

        var transformed = ApplyResponseTransformers("tools/call", result);
        return new McpToolCallResult(transformed);
    }

    /// <summary>
    /// Starts a new Codex session by invoking the MCP tool that wraps Codex.
    /// </summary>
    public async Task<CodexMcpSessionStartResult> StartSessionAsync(CodexMcpStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Prompt))
            throw new ArgumentException("Prompt is required.", nameof(options));

        var args = new Dictionary<string, object>
        {
            ["prompt"] = options.Prompt
        };

        if (options.ApprovalPolicy is { } approvalPolicy)
        {
            args["approval-policy"] = approvalPolicy.ToMcpWireValue();
        }

        if (options.Sandbox is { } sandbox)
        {
            args["sandbox"] = sandbox.ToMcpWireValue();
        }

        if (!string.IsNullOrWhiteSpace(options.Cwd))
        {
            args["cwd"] = options.Cwd;
        }

        if (options.Model is { } model)
        {
            args["model"] = model.Value;
        }

        if (options.IncludePlanTool is { } includePlanTool)
        {
            args["include-plan-tool"] = includePlanTool;
        }

        var call = await CallToolAsync("codex", args, ct);
        var parsed = ParseCodexToolResult("codex", call.Raw);
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
        var parsed = ParseCodexToolResult("codex-reply", call.Raw);
        return new CodexMcpReplyResult(parsed.ThreadId, parsed.Text, parsed.StructuredContent, parsed.Raw);
    }

    private static string Truncate(string value, int maxChars)
    {
        if (value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars] + "...";
    }

    private JsonElement ApplyResponseTransformers(string method, JsonElement result)
    {
        var transformers = _options.ResponseTransformers;
        if (transformers is not { Count: > 0 })
        {
            return result;
        }

        var transformed = result;
        foreach (var transformer in transformers)
        {
            if (transformer is null)
            {
                continue;
            }

            try
            {
                transformed = transformer.Transform(method, transformed);
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "MCP response transformer threw (method={Method}).", method);
            }
        }

        return transformed;
    }

    private CodexMcpToolParsedResult ParseCodexToolResult(string toolName, JsonElement raw)
    {
        var transformed = raw;

        var transformers = _options.CodexToolResultTransformers;
        if (transformers is { Count: > 0 })
        {
            foreach (var transformer in transformers)
            {
                if (transformer is null)
                {
                    continue;
                }

                try
                {
                    transformed = transformer.Transform(toolName, transformed);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Codex MCP tool result transformer threw (tool={ToolName}).", toolName);
                }
            }
        }

        var mappers = _options.CodexToolResultMappers;
        if (mappers is { Count: > 0 })
        {
            foreach (var mapper in mappers)
            {
                if (mapper is null)
                {
                    continue;
                }

                try
                {
                    var mapped = mapper.TryMap(toolName, transformed);
                    if (mapped is not null)
                    {
                        return mapped;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Codex MCP tool result mapper threw (tool={ToolName}).", toolName);
                }
            }
        }

        var parsed = CodexMcpResultParser.Parse(transformed);
        return new CodexMcpToolParsedResult(parsed.ThreadId, parsed.Text, parsed.StructuredContent, parsed.Raw);
    }

    internal async Task InitializeAsync(CancellationToken ct)
    {
        var clientInfo = _options.ClientInfo;
        object capabilities = _options.ElicitationHandler is null
            ? new { }
            : new
            {
                elicitation = new
                {
                    // Matches the MCP capability surface used by upstream rmcp clients.
                    form = new { schemaValidation = (bool?)null },
                    url = (object?)null
                }
            };

        await _rpc.SendRequestAsync(
            "initialize",
            new
            {
                protocolVersion = "2025-06-18",
                clientInfo = new { name = clientInfo.Name, title = clientInfo.Title, version = clientInfo.Version },
                capabilities
            },
            ct);

        await _rpc.SendNotificationAsync("notifications/initialized", @params: null, ct);
    }

    private async ValueTask<JsonRpcResponse> OnRpcServerRequestAsync(JsonRpcRequest req)
    {
        var handler = _options.ElicitationHandler;
        if (handler is null)
        {
            if (string.Equals(req.Method, "elicitation/create", StringComparison.OrdinalIgnoreCase))
            {
                return new JsonRpcResponse(req.Id, Result: DefaultElicitationDeniedResult, Error: null);
            }

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

    private static JsonElement CreateDefaultElicitationDeniedResult()
    {
        using var doc = JsonDocument.Parse("{\"decision\":\"denied\"}");
        return doc.RootElement.Clone();
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
