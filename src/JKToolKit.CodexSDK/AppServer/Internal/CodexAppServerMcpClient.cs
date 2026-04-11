using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerMcpClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;

    public CodexAppServerMcpClient(Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
    }

    public async Task ReloadMcpServersAsync(CancellationToken ct = default)
    {
        _ = await _sendRequestAsync(
            "config/mcpServer/reload",
            null,
            ct);
    }

    public async Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "mcpServerStatus/list",
            new ListMcpServerStatusParams
            {
                Cursor = options.Cursor,
                Limit = options.Limit,
                Detail = options.Detail?.Value
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(result);
    }

    public async Task<McpResourceReadResult> ReadMcpResourceAsync(McpResourceReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Server))
            throw new ArgumentException("Server cannot be empty or whitespace.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Uri))
            throw new ArgumentException("Uri cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "mcpResource/read",
            new UpstreamV2.McpResourceReadParams
            {
                Server = options.Server,
                ThreadId = options.ThreadId,
                Uri = options.Uri
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpResourceReadResult(result);
    }

    public async Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
            throw new ArgumentException("Name cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "mcpServer/oauth/login",
            new McpServerOauthLoginParams
            {
                Name = options.Name,
                Scopes = options.Scopes,
                TimeoutSecs = options.TimeoutSeconds
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpServerOauthLoginResult(result);
    }
}

