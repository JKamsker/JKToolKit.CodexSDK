using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientConfigReadParsers
{
    public static ConfigReadResult ParseConfigReadResult(JsonElement result)
    {
        var config = TryGetObject(result, JsonFieldNames.Config) ?? result;
        var layers = ParseLayers(result);
        var origins = ParseOrigins(result);
        var mcpServers = ParseMcpServers(config);

        return new ConfigReadResult
        {
            Config = config,
            Origins = origins,
            Layers = layers,
            McpServers = mcpServers,
            Raw = result
        };
    }

    private static IReadOnlyList<ConfigLayerInfo>? ParseLayers(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("layers", out var layersProp))
        {
            return null;
        }

        if (layersProp.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (layersProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var layers = new List<ConfigLayerInfo>();
        foreach (var layer in layersProp.EnumerateArray())
        {
            if (layer.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var nameObj = TryGetObject(layer, JsonFieldNames.Name);
            var version = GetStringOrNull(layer, JsonFieldNames.Version);
            if (nameObj is null || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            layers.Add(new ConfigLayerInfo
            {
                Name = ParseLayerSource(nameObj.Value),
                Version = version,
                Config = layer.TryGetProperty(JsonFieldNames.Config, out var cfg) ? cfg : default,
                DisabledReason = GetStringOrNull(layer, JsonFieldNames.DisabledReason) ?? GetStringOrNull(layer, "disabled_reason"),
                Raw = layer
            });
        }

        return layers;
    }

    private static IReadOnlyDictionary<string, ConfigLayerMetadataInfo>? ParseOrigins(JsonElement result)
    {
        var originsObj = TryGetObject(result, JsonFieldNames.Origins);
        if (originsObj is null)
        {
            return null;
        }

        var dict = new Dictionary<string, ConfigLayerMetadataInfo>(StringComparer.Ordinal);
        foreach (var p in originsObj.Value.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            dict[p.Name] = ParseConfigLayerMetadataInfo(p.Value, $"config/read origins['{p.Name}']");
        }

        return dict;
    }

    public static ConfigLayerMetadataInfo ParseConfigLayerMetadataInfo(JsonElement metadata, string context)
    {
        var nameObj = TryGetObject(metadata, JsonFieldNames.Name)
            ?? throw new InvalidOperationException($"Missing required object property 'name' on {context}.");
        var version = GetStringOrNull(metadata, JsonFieldNames.Version);
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException($"Missing required string property 'version' on {context}.");
        }

        return new ConfigLayerMetadataInfo
        {
            Name = ParseLayerSource(nameObj),
            Version = version,
            Raw = metadata
        };
    }

    private static IReadOnlyDictionary<string, McpServerConfigInfo>? ParseMcpServers(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!config.TryGetProperty("mcp_servers", out var mcpServersObj) || mcpServersObj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, McpServerConfigInfo>(StringComparer.Ordinal);
        foreach (var serverProp in mcpServersObj.EnumerateObject())
        {
            if (serverProp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            dict[serverProp.Name] = ParseMcpServerConfig(serverProp.Value);
        }

        return dict;
    }

    private static McpServerConfigInfo ParseMcpServerConfig(JsonElement obj)
    {
        var hasCommand = GetStringOrNull(obj, JsonFieldNames.Command);
        var hasUrl = GetStringOrNull(obj, JsonFieldNames.Url);

        var transport =
            !string.IsNullOrWhiteSpace(hasCommand) ? "stdio" :
            !string.IsNullOrWhiteSpace(hasUrl) ? "streamableHttp" :
            "unknown";

        return new McpServerConfigInfo
        {
            Transport = transport,
            Command = hasCommand,
            Args = GetOptionalStringArray(obj, "args"),
            Env = TryGetStringDictionary(obj, JsonFieldNames.Env),
            EnvVars = GetOptionalStringArray(obj, "env_vars") ?? GetOptionalStringArray(obj, "envVars"),
            Cwd = GetStringOrNull(obj, JsonFieldNames.Cwd),
            Url = hasUrl,
            BearerTokenEnvVar = GetStringOrNull(obj, "bearer_token_env_var") ?? GetStringOrNull(obj, "bearerTokenEnvVar"),
            HttpHeaders = TryGetStringDictionary(obj, "http_headers") ?? TryGetStringDictionary(obj, "httpHeaders"),
            EnvHttpHeaders = TryGetStringDictionary(obj, "env_http_headers") ?? TryGetStringDictionary(obj, "envHttpHeaders"),
            Enabled = GetBoolOrNull(obj, JsonFieldNames.Enabled),
            Required = GetBoolOrNull(obj, JsonFieldNames.Required),
            StartupTimeout = GetTimeSpanSecondsOrNull(obj, "startup_timeout_sec") ?? GetTimeSpanSecondsOrNull(obj, "startupTimeoutSec"),
            ToolTimeout = GetTimeSpanSecondsOrNull(obj, "tool_timeout_sec") ?? GetTimeSpanSecondsOrNull(obj, "toolTimeoutSec"),
            EnabledTools = GetOptionalStringArray(obj, "enabled_tools") ?? GetOptionalStringArray(obj, "enabledTools"),
            DisabledTools = GetOptionalStringArray(obj, "disabled_tools") ?? GetOptionalStringArray(obj, "disabledTools"),
            Scopes = GetOptionalStringArray(obj, JsonFieldNames.Scopes),
            Raw = obj
        };
    }

    private static TimeSpan? GetTimeSpanSecondsOrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d))
        {
            if (d < 0)
            {
                return null;
            }

            return TimeSpan.FromSeconds(d);
        }

        if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), out var s) && s >= 0)
        {
            return TimeSpan.FromSeconds(s);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? TryGetStringDictionary(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (p.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in p.EnumerateObject())
        {
            if (kv.Value.ValueKind == JsonValueKind.String)
            {
                dict[kv.Name] = kv.Value.GetString() ?? string.Empty;
            }
        }

        return dict;
    }

    private static ConfigLayerSourceInfo ParseLayerSource(JsonElement obj)
    {
        var type = GetStringOrNull(obj, JsonFieldNames.Type) ?? "unknown";

        return new ConfigLayerSourceInfo
        {
            Type = type,
            Id = GetStringOrNull(obj, JsonFieldNames.Id),
            Name = GetStringOrNull(obj, JsonFieldNames.Name),
            Domain = GetStringOrNull(obj, "domain"),
            Key = GetStringOrNull(obj, JsonFieldNames.Key),
            File = GetStringOrNull(obj, "file"),
            Profile = GetStringOrNull(obj, JsonFieldNames.Profile),
            DotCodexFolder = GetStringOrNull(obj, "dotCodexFolder") ?? GetStringOrNull(obj, "dot_codex_folder"),
            Raw = obj
        };
    }
}

