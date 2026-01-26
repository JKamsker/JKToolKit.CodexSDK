using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NCodexSDK.Abstractions;
using NCodexSDK.Infrastructure;
using NCodexSDK.Infrastructure.JsonRpc;
using NCodexSDK.Infrastructure.Stdio;
using NCodexSDK.Public.Models;

namespace NCodexSDK.McpServer;

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

    public static async Task<CodexMcpServerClient> StartAsync(
        CodexMcpServerClientOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loggerFactory = NullLoggerFactory.Instance;

        var fileSystem = new RealFileSystem();
        var pathProviderLogger = loggerFactory.CreateLogger<DefaultCodexPathProvider>();
        ICodexPathProvider pathProvider = new DefaultCodexPathProvider(fileSystem, pathProviderLogger);

        var stdioFactory = new StdioProcessFactory(pathProvider, loggerFactory.CreateLogger<StdioProcessFactory>());

        var process = await stdioFactory.StartAsync(
            options.Launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            ct);

        var rpc = new JsonRpcConnection(
            reader: process.Stdout,
            writer: process.Stdin,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: options.NotificationBufferCapacity,
            serializerOptions: options.SerializerOptionsOverride,
            logger: loggerFactory.CreateLogger<JsonRpcConnection>());

        var client = new CodexMcpServerClient(options, process, rpc);
        await client.InitializeAsync(ct);
        return client;
    }

    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _rpc.SendRequestAsync(method, @params, ct);

    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await _rpc.SendRequestAsync("tools/list", @params: null, ct);

        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("tools", out var toolsProp) || toolsProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<McpToolDescriptor>();
        }

        var list = new List<McpToolDescriptor>();
        foreach (var tool in toolsProp.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object) continue;

            var name = tool.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var description = tool.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

            JsonElement? schema = null;
            if (tool.TryGetProperty("inputSchema", out var schemaProp))
            {
                schema = schemaProp.Clone();
            }

            list.Add(new McpToolDescriptor(name!, description, schema));
        }

        return list;
    }

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
        return ParseCodexResult(call.Raw);
    }

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

        var parsed = ParseCodexResult(call.Raw);
        return new CodexMcpReplyResult(parsed.ThreadId, parsed.Text, parsed.StructuredContent, parsed.Raw);
    }

    private async Task InitializeAsync(CancellationToken ct)
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

    private static CodexMcpSessionStartResult ParseCodexResult(JsonElement raw)
    {
        var structured = TryGet(raw, "structuredContent") ?? TryGet(raw, "structured_content");
        var content = TryGet(raw, "content");

        var threadId =
            (structured is { } s && TryGetString(s, "threadId") is { Length: > 0 } sid) ? sid :
            (structured is { } s2 && TryGetString(s2, "conversationId") is { Length: > 0 } cid) ? cid :
            string.Empty;

        var text = TryExtractText(content, structured);

        var structuredElement = structured ?? JsonDocument.Parse("{}").RootElement.Clone();

        return new CodexMcpSessionStartResult(threadId, text, structuredElement, raw);
    }

    private static string? TryExtractText(JsonElement? content, JsonElement? structured)
    {
        // Best effort: MCP content is commonly an array of blocks with { type, text }.
        if (content is { ValueKind: JsonValueKind.Array })
        {
            foreach (var item in content.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                if (item.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    return textProp.GetString();
                }
            }
        }

        if (structured is { } s && s.ValueKind == JsonValueKind.Object)
        {
            if (s.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                return contentProp.GetString();
            }
        }

        return null;
    }

    private static JsonElement? TryGet(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var prop)
            ? prop.Clone()
            : null;

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

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

