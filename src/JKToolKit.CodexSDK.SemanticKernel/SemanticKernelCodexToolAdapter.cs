using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.SemanticKernel;

namespace JKToolKit.CodexSDK.SemanticKernel;

/// <summary>
/// Converts Semantic Kernel plugins into Codex app-server dynamic tools.
/// </summary>
public static class SemanticKernelCodexToolAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates Codex dynamic tools for all plugins currently registered on a kernel.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance that owns the functions.</param>
    /// <param name="fallbackHandler">Optional handler for app-server requests not handled by the adapter.</param>
    public static SemanticKernelCodexToolSet Create(
        Kernel kernel,
        IAppServerApprovalHandler? fallbackHandler = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        return Create(kernel, kernel.Plugins, fallbackHandler);
    }

    /// <summary>
    /// Creates Codex dynamic tools for the supplied Semantic Kernel plugins.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance used to invoke the functions.</param>
    /// <param name="plugins">The plugins to expose to Codex.</param>
    /// <param name="fallbackHandler">Optional handler for app-server requests not handled by the adapter.</param>
    public static SemanticKernelCodexToolSet Create(
        Kernel kernel,
        IEnumerable<KernelPlugin> plugins,
        IAppServerApprovalHandler? fallbackHandler = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(plugins);

        var functions = plugins.SelectMany(plugin => plugin).ToArray();
        var tools = functions.Select(CreateDynamicTool).ToArray();
        var handler = new SemanticKernelToolCallHandler(kernel, functions, fallbackHandler);

        return new SemanticKernelCodexToolSet(tools, handler);
    }

    internal static string GetToolName(KernelFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return string.IsNullOrWhiteSpace(function.PluginName)
            ? function.Name
            : $"{function.PluginName}-{function.Name}";
    }

    private static DynamicToolSpec CreateDynamicTool(KernelFunction function)
    {
        return new DynamicToolSpec
        {
            Name = GetToolName(function),
            Description = GetDescription(function),
            InputSchema = CreateInputSchema(function)
        };
    }

    private static string GetDescription(KernelFunction function)
    {
        if (!string.IsNullOrWhiteSpace(function.Description))
        {
            return function.Description;
        }

        return string.IsNullOrWhiteSpace(function.PluginName)
            ? function.Name
            : $"{function.PluginName}.{function.Name}";
    }

    private static JsonElement CreateInputSchema(KernelFunction function)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var parameter in function.Metadata.Parameters)
        {
            properties[parameter.Name] = GetParameterSchema(parameter);
            if (parameter.IsRequired)
            {
                required.Add(parameter.Name);
            }
        }

        return JsonSerializer.SerializeToElement(
            new JsonSchemaObject("object", properties, required),
            SerializerOptions);
    }

    private static JsonElement GetParameterSchema(KernelParameterMetadata parameter)
    {
        if (parameter.Schema?.RootElement is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } schema)
        {
            return schema.Clone();
        }

        return JsonSerializer.SerializeToElement(new JsonSchemaFallback(GetJsonType(parameter.ParameterType)), SerializerOptions);
    }

    private static string GetJsonType(Type? type)
    {
        type = Nullable.GetUnderlyingType(type ?? typeof(object)) ?? type ?? typeof(object);

        if (type == typeof(string) || type.IsEnum)
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
        {
            return "integer";
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return "number";
        }

        return "object";
    }

    private sealed record JsonSchemaObject(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, JsonElement> Properties,
        [property: JsonPropertyName("required")] IReadOnlyList<string> Required);

    private sealed record JsonSchemaFallback([property: JsonPropertyName("type")] string Type);
}
