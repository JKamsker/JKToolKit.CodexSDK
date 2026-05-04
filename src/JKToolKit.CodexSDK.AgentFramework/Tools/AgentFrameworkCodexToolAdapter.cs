using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Tools;

/// <summary>
/// Converts Microsoft Agent Framework functions into Codex app-server dynamic tools.
/// </summary>
public static class AgentFrameworkCodexToolAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates Codex dynamic tools for the supplied Microsoft Agent Framework functions.
    /// </summary>
    /// <param name="functions">The invocable AI functions to expose to Codex.</param>
    /// <param name="fallbackHandler">Optional handler for app-server requests not handled by the adapter.</param>
    public static AgentFrameworkCodexToolSet Create(
        IEnumerable<AIFunction> functions,
        IAppServerApprovalHandler? fallbackHandler = null)
    {
        ArgumentNullException.ThrowIfNull(functions);

        var functionArray = functions.ToArray();
        var tools = functionArray.Select(CreateDynamicTool).ToArray();
        var handler = new AgentFrameworkToolCallHandler(functionArray, fallbackHandler);

        return new AgentFrameworkCodexToolSet(tools, handler);
    }

    private static DynamicToolSpec CreateDynamicTool(AIFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return new DynamicToolSpec
        {
            Name = function.Name,
            Description = GetDescription(function),
            InputSchema = CreateInputSchema(function)
        };
    }

    private static string GetDescription(AIFunction function)
    {
        return string.IsNullOrWhiteSpace(function.Description)
            ? function.Name
            : function.Description;
    }

    private static JsonElement CreateInputSchema(AIFunction function)
    {
        var schema = function.JsonSchema;
        if (schema.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            return schema.Clone();
        }

        return JsonSerializer.SerializeToElement(new EmptyObjectSchema("object"), SerializerOptions);
    }

    private sealed record EmptyObjectSchema([property: JsonPropertyName("type")] string Type);
}
