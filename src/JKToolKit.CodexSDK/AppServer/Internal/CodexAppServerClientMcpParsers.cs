using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientMcpParsers
{
    public static McpServerStatusListPage ParseMcpServerStatusListPage(JsonElement result)
    {
        var servers = new List<McpServerStatusInfo>();

        var data = TryGetArray(result, JsonFieldNames.Data);
        if (data is not null && data.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = GetStringOrNull(item, JsonFieldNames.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var authStatus = ParseAuthStatus(GetStringOrNull(item, "authStatus") ?? GetStringOrNull(item, "auth_status"));

                var tools = ParseTools(item);
                var resources = ParseResources(item);
                var templates = ParseResourceTemplates(item);

                servers.Add(new McpServerStatusInfo
                {
                    Name = name,
                    PluginId = GetStringOrNull(item, JsonFieldNames.PluginId) ?? GetStringOrNull(item, "plugin_id"),
                    HttpOrigin = GetStringOrNull(item, "httpOrigin") ?? GetStringOrNull(item, "http_origin"),
                    AuthStatus = authStatus,
                    RuntimeStatus = ParseRuntimeStatus(GetStringOrNull(item, "runtimeStatus") ?? GetStringOrNull(item, "runtime_status")),
                    StartupStatus = GetStringOrNull(item, JsonFieldNames.Status),
                    Error = GetStringOrNull(item, JsonFieldNames.Error),
                    FailureReason = McpServerStartupFailureReason.TryParse(GetStringOrNull(item, "failureReason"), out var failureReason)
                        ? (McpServerStartupFailureReason?)failureReason
                        : null,
                    ServerInfo = ParseServerInfo(item),
                    ServerCapabilities = TryGetObject(item, "serverCapabilities")?.Clone(),
                    Tools = tools,
                    ToolsError = GetStringOrNull(item, "toolsError") ?? GetStringOrNull(item, "tools_error"),
                    Resources = resources,
                    ResourceTemplates = templates,
                    Raw = item
                });
            }
        }

        return new McpServerStatusListPage
        {
            Servers = servers,
            NextCursor = GetStringOrNull(result, JsonFieldNames.NextCursor) ?? GetStringOrNull(result, JsonFieldNames.SnakeCase.NextCursor),
            Raw = result
        };
    }

    private static McpServerRuntimeStatus ParseRuntimeStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return McpServerRuntimeStatus.Unknown;
        }

        return value.Trim() switch
        {
            "notStarted" => McpServerRuntimeStatus.NotStarted,
            "starting" => McpServerRuntimeStatus.Starting,
            "connected" => McpServerRuntimeStatus.Connected,
            "authenticationRequired" => McpServerRuntimeStatus.AuthenticationRequired,
            "failed" => McpServerRuntimeStatus.Failed,
            "cancelled" => McpServerRuntimeStatus.Cancelled,
            "disabled" => McpServerRuntimeStatus.Disabled,
            _ => McpServerRuntimeStatus.Unknown
        };
    }

    private static McpServerImplementationInfo? ParseServerInfo(JsonElement statusObj)
    {
        if (TryGetObject(statusObj, "serverInfo") is not { } serverInfo)
        {
            return null;
        }

        return new McpServerImplementationInfo
        {
            Name = GetStringOrNull(serverInfo, JsonFieldNames.Name),
            Version = GetStringOrNull(serverInfo, JsonFieldNames.Version),
            Title = GetStringOrNull(serverInfo, JsonFieldNames.Title),
            Description = GetStringOrNull(serverInfo, JsonFieldNames.Description),
            WebsiteUrl = GetStringOrNull(serverInfo, JsonFieldNames.WebsiteUrl) ?? GetStringOrNull(serverInfo, "website_url"),
            Raw = serverInfo.Clone()
        };
    }

    public static McpServerOauthLoginResult ParseMcpServerOauthLoginResult(JsonElement result)
    {
        var url = GetStringOrNull(result, "authorizationUrl") ?? GetStringOrNull(result, "authorization_url");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                $"mcpServer/oauth/login returned no authorizationUrl. Raw result: {result}");
        }

        return new McpServerOauthLoginResult
        {
            AuthorizationUrl = url,
            Raw = result
        };
    }

    public static McpResourceReadResult ParseMcpResourceReadResult(JsonElement result)
    {
        var contentsArray = TryGetArray(result, "contents");
        if (contentsArray is null || contentsArray.Value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("mcpResource/read response must contain a contents array.");
        }

        var contents = new List<McpResourceContent>();
        foreach (var item in contentsArray.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("mcpResource/read response contents[] must contain only objects.");
            }

            var uri = GetStringOrNull(item, JsonFieldNames.Uri);
            if (string.IsNullOrWhiteSpace(uri))
            {
                throw new InvalidOperationException("mcpResource/read response contents[] must contain a non-empty uri.");
            }

            var text = TryGetAny(item, JsonFieldNames.Text);
            var blob = TryGetAny(item, "blob");
            var hasText = text is { ValueKind: JsonValueKind.String };
            var hasBlob = blob is { ValueKind: JsonValueKind.String };

            if (hasText == hasBlob)
            {
                throw new InvalidOperationException(
                    "mcpResource/read response contents[] must contain exactly one of text or blob.");
            }

            contents.Add(new McpResourceContent
            {
                Uri = uri,
                MimeType = GetStringOrNull(item, JsonFieldNames.MimeType) ?? GetStringOrNull(item, JsonFieldNames.SnakeCase.MimeType),
                Text = hasText ? text!.Value.GetString() : null,
                BlobBase64 = hasBlob ? blob!.Value.GetString() : null,
                Raw = item.Clone()
            });
        }

        return new McpResourceReadResult
        {
            Contents = contents,
            OriginCallId = GetStringOrNull(result, "originCallId") ?? GetStringOrNull(result, "origin_call_id"),
            Raw = result
        };
    }

    private static McpAuthStatus ParseAuthStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return McpAuthStatus.Unknown;
        }

        var normalized = value.Trim();

        return normalized switch
        {
            "unsupported" => McpAuthStatus.Unsupported,
            "notLoggedIn" => McpAuthStatus.NotLoggedIn,
            "bearerToken" => McpAuthStatus.BearerToken,
            "oAuth" => McpAuthStatus.OAuth,
            "oauth" => McpAuthStatus.OAuth,
            _ => McpAuthStatus.Unknown
        };
    }

    private static IReadOnlyList<McpServerToolInfo> ParseTools(JsonElement statusObj)
    {
        var toolsObj = TryGetObject(statusObj, JsonFieldNames.Tools);
        if (toolsObj is null || toolsObj.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<McpServerToolInfo>();
        }

        var tools = new List<McpServerToolInfo>();
        foreach (var p in toolsObj.Value.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var toolName = GetStringOrNull(p.Value, JsonFieldNames.Name) ?? p.Name;
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            // inputSchema is required in the upstream Tool definition; skip malformed entries.
            var inputSchema = TryGetAny(p.Value, JsonFieldNames.InputSchema) ?? TryGetAny(p.Value, "input_schema");
            if (inputSchema is null)
            {
                continue;
            }

            var outputSchema = TryGetAny(p.Value, JsonFieldNames.OutputSchema) ?? TryGetAny(p.Value, "output_schema");

            tools.Add(new McpServerToolInfo
            {
                Name = toolName,
                Title = GetStringOrNull(p.Value, JsonFieldNames.Title),
                Description = GetStringOrNull(p.Value, JsonFieldNames.Description),
                InputSchema = inputSchema,
                OutputSchema = outputSchema,
                Raw = p.Value
            });
        }

        return tools;
    }

    private static JsonElement? TryGetAny(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? p
            : null;

    private static IReadOnlyList<McpServerResourceInfo> ParseResources(JsonElement statusObj)
    {
        var array = TryGetArray(statusObj, "resources");
        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<McpServerResourceInfo>();
        }

        var resources = new List<McpServerResourceInfo>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetStringOrNull(item, JsonFieldNames.Name);
            var uri = GetStringOrNull(item, JsonFieldNames.Uri);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uri))
            {
                continue;
            }

            resources.Add(new McpServerResourceInfo
            {
                Name = name,
                Uri = uri,
                Title = GetStringOrNull(item, JsonFieldNames.Title),
                Description = GetStringOrNull(item, JsonFieldNames.Description),
                MimeType = GetStringOrNull(item, JsonFieldNames.MimeType) ?? GetStringOrNull(item, JsonFieldNames.SnakeCase.MimeType),
                Size = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("size", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number && sizeProp.TryGetInt64(out var size)
                    ? size
                    : null,
                Raw = item
            });
        }

        return resources;
    }

    private static IReadOnlyList<McpServerResourceTemplateInfo> ParseResourceTemplates(JsonElement statusObj)
    {
        var array =
            TryGetArray(statusObj, "resourceTemplates") ??
            TryGetArray(statusObj, "resource_templates");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<McpServerResourceTemplateInfo>();
        }

        var templates = new List<McpServerResourceTemplateInfo>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetStringOrNull(item, JsonFieldNames.Name);
            var uriTemplate = GetStringOrNull(item, "uriTemplate") ?? GetStringOrNull(item, "uri_template");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uriTemplate))
            {
                continue;
            }

            templates.Add(new McpServerResourceTemplateInfo
            {
                Name = name,
                UriTemplate = uriTemplate,
                Title = GetStringOrNull(item, JsonFieldNames.Title),
                Description = GetStringOrNull(item, JsonFieldNames.Description),
                MimeType = GetStringOrNull(item, JsonFieldNames.MimeType) ?? GetStringOrNull(item, JsonFieldNames.SnakeCase.MimeType),
                Raw = item
            });
        }

        return templates;
    }
}
