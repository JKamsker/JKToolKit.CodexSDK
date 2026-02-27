using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.McpServer;

public sealed partial class CodexMcpServerClient
{
    private readonly SemaphoreSlim _toolSchemasGate = new(1, 1);
    private Dictionary<string, HashSet<string>>? _allowedArgumentKeysByToolName;

    private async Task<IDictionary<string, object?>> GateArgumentsAsync(
        string toolName,
        IDictionary<string, object?> arguments,
        CancellationToken ct)
    {
        var allowedKeys = await GetAllowedArgumentKeysAsync(toolName, ct).ConfigureAwait(false);
        if (allowedKeys is null)
        {
            return arguments;
        }

        var filtered = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in arguments)
        {
            if (allowedKeys.Contains(key))
            {
                filtered[key] = value;
            }
        }

        if (filtered.Count != arguments.Count)
        {
            _logger.LogDebug(
                "Filtered {DroppedCount} unknown tool argument(s) for tool '{ToolName}'.",
                arguments.Count - filtered.Count,
                toolName);
        }

        return filtered;
    }

    private async Task<HashSet<string>?> GetAllowedArgumentKeysAsync(string toolName, CancellationToken ct)
    {
        if (_allowedArgumentKeysByToolName is not null &&
            _allowedArgumentKeysByToolName.TryGetValue(toolName, out var keys))
        {
            return keys;
        }

        await _toolSchemasGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_allowedArgumentKeysByToolName is null)
            {
                _allowedArgumentKeysByToolName = await LoadAllowedArgumentKeysByToolNameAsync(ct).ConfigureAwait(false);
            }

            return _allowedArgumentKeysByToolName.TryGetValue(toolName, out keys) ? keys : null;
        }
        finally
        {
            _toolSchemasGate.Release();
        }
    }

    private async Task<Dictionary<string, HashSet<string>>> LoadAllowedArgumentKeysByToolNameAsync(CancellationToken ct)
    {
        const int maxPages = 100;
        var tools = new List<McpToolDescriptor>();
        string? cursor = null;

        for (var i = 0; i < maxPages; i++)
        {
            var result = await _rpc.SendRequestAsync(
                "tools/list",
                @params: i == 0 ? null : new { cursor },
                ct).ConfigureAwait(false);

            var transformed = ApplyResponseTransformers("tools/list", result);

            if (!Internal.McpToolsListParser.TryParse(transformed, out var pageTools, out var nextCursor))
            {
                if (_options.StrictParsing)
                {
                    throw new JsonException("Unexpected tools/list result shape.");
                }

                _logger.LogWarning("Unexpected tools/list result shape: {Result}", Truncate(transformed.GetRawText(), maxChars: 4000));
                break;
            }

            tools.AddRange(pageTools);

            if (string.IsNullOrWhiteSpace(nextCursor))
            {
                break;
            }

            cursor = nextCursor;
        }

        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            if (tool.InputSchema is not { } schema)
            {
                continue;
            }

            if (!TryExtractSchemaPropertyNames(schema, out var properties))
            {
                continue;
            }

            map[tool.Name] = properties;
        }

        return map;
    }

    private static bool TryExtractSchemaPropertyNames(JsonElement schema, out HashSet<string> propertyNames)
    {
        propertyNames = new HashSet<string>(StringComparer.Ordinal);

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Only gate arguments when the schema is explicitly closed. In JSON Schema,
        // additionalProperties defaults to allowed (true), so treat an absent field as open.
        if (!schema.TryGetProperty("additionalProperties", out var additionalProperties) ||
            additionalProperties.ValueKind != JsonValueKind.False)
        {
            return false;
        }

        if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in props.EnumerateObject())
            {
                propertyNames.Add(p.Name);
            }
        }

        return true;
    }
}
