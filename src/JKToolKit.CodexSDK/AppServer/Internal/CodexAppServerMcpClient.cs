using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

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
                Limit = options.Limit
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(result);
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

