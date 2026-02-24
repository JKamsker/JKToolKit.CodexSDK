using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientMcpParsers
{
    public static McpServerStatusListPage ParseMcpServerStatusListPage(JsonElement result)
    {
        var servers = new List<McpServerStatusInfo>();

        var data = TryGetArray(result, "data");
        if (data is not null && data.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = GetStringOrNull(item, "name");
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
                    AuthStatus = authStatus,
                    Tools = tools,
                    Resources = resources,
                    ResourceTemplates = templates,
                    Raw = item
                });
            }
        }

        return new McpServerStatusListPage
        {
            Servers = servers,
            NextCursor = GetStringOrNull(result, "nextCursor") ?? GetStringOrNull(result, "next_cursor"),
            Raw = result
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
        var toolsObj = TryGetObject(statusObj, "tools");
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

            var toolName = GetStringOrNull(p.Value, "name") ?? p.Name;
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            var inputSchema = TryGetAny(p.Value, "inputSchema") ?? TryGetAny(p.Value, "input_schema");
            var outputSchema = TryGetAny(p.Value, "outputSchema") ?? TryGetAny(p.Value, "output_schema");

            tools.Add(new McpServerToolInfo
            {
                Name = toolName,
                Title = GetStringOrNull(p.Value, "title"),
                Description = GetStringOrNull(p.Value, "description"),
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

            var name = GetStringOrNull(item, "name");
            var uri = GetStringOrNull(item, "uri");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uri))
            {
                continue;
            }

            resources.Add(new McpServerResourceInfo
            {
                Name = name,
                Uri = uri,
                Title = GetStringOrNull(item, "title"),
                Description = GetStringOrNull(item, "description"),
                MimeType = GetStringOrNull(item, "mimeType") ?? GetStringOrNull(item, "mime_type"),
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

            var name = GetStringOrNull(item, "name");
            var uriTemplate = GetStringOrNull(item, "uriTemplate") ?? GetStringOrNull(item, "uri_template");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uriTemplate))
            {
                continue;
            }

            templates.Add(new McpServerResourceTemplateInfo
            {
                Name = name,
                UriTemplate = uriTemplate,
                Title = GetStringOrNull(item, "title"),
                Description = GetStringOrNull(item, "description"),
                MimeType = GetStringOrNull(item, "mimeType") ?? GetStringOrNull(item, "mime_type"),
                Raw = item
            });
        }

        return templates;
    }
}
