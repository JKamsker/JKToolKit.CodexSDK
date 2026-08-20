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
                Detail = options.Detail?.Value,
                ThreadId = options.ThreadId
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(result);
    }

    public async Task<McpResourceReadResult> ReadMcpResourceAsync(McpResourceReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Server))
            throw new ArgumentException("Server cannot be empty or whitespace.", nameof(options));
        ValidateOptionalWireValue(options.ThreadId, nameof(options.ThreadId), nameof(options));
        ValidateOptionalWireValue(options.OriginCallId, nameof(options.OriginCallId), nameof(options));
        ValidateOptionalWireValue(options.ConnectorId, nameof(options.ConnectorId), nameof(options));
        if (!string.IsNullOrWhiteSpace(options.OriginCallId) && string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("OriginCallId requires ThreadId.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Uri))
            throw new ArgumentException("Uri cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "mcpResource/read",
            new UpstreamV2.McpResourceReadParams
            {
                ConnectorId = options.ConnectorId,
                OriginCallId = options.OriginCallId,
                Server = options.Server,
                ThreadId = options.ThreadId,
                Uri = options.Uri
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpResourceReadResult(result);
    }

    private static void ValidateOptionalWireValue(string? value, string displayName, string paramName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }
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
                ThreadId = options.ThreadId,
                ClientRegistration = options.ClientRegistration?.Value,
                Scopes = options.Scopes,
                TimeoutSecs = options.TimeoutSeconds
            },
            ct);

        return CodexAppServerClientMcpParsers.ParseMcpServerOauthLoginResult(result);
    }
}
