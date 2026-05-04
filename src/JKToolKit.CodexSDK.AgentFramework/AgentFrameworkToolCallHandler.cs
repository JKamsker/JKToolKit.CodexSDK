using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework;

/// <summary>
/// Handles Codex app-server dynamic tool calls by invoking Agent Framework functions.
/// </summary>
public sealed class AgentFrameworkToolCallHandler : IAppServerApprovalHandler
{
    private const string DynamicToolCallMethod = "item/tool/call";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IReadOnlyDictionary<string, AIFunction> _functionsByToolName;
    private readonly IAppServerApprovalHandler? _fallbackHandler;

    /// <summary>
    /// Creates a handler for Codex app-server dynamic tool calls.
    /// </summary>
    /// <param name="functions">The functions exposed as dynamic tools.</param>
    /// <param name="fallbackHandler">Optional handler for app-server requests not handled by this adapter.</param>
    public AgentFrameworkToolCallHandler(
        IEnumerable<AIFunction> functions,
        IAppServerApprovalHandler? fallbackHandler = null)
    {
        ArgumentNullException.ThrowIfNull(functions);

        _functionsByToolName = BuildFunctionMap(functions);
        _fallbackHandler = fallbackHandler;
    }

    /// <inheritdoc />
    public async ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        if (!string.Equals(method, DynamicToolCallMethod, StringComparison.Ordinal))
        {
            return _fallbackHandler is not null
                ? await _fallbackHandler.HandleAsync(method, @params, ct)
                : throw new NotSupportedException($"Unhandled app-server request '{method}'.");
        }

        var request = ReadRequest(@params);
        if (!_functionsByToolName.TryGetValue(request.Tool, out var function))
        {
            return CreateResponse(success: false, $"Unknown Agent Framework tool '{request.Tool}'.");
        }

        try
        {
            var result = await function.InvokeAsync(CreateArguments(request.Arguments), ct);
            return CreateResponse(success: true, FormatResult(result, function.JsonSerializerOptions));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CreateResponse(success: false, ex.Message);
        }
    }

    private static IReadOnlyDictionary<string, AIFunction> BuildFunctionMap(IEnumerable<AIFunction> functions)
    {
        var map = new Dictionary<string, AIFunction>(StringComparer.Ordinal);
        foreach (var function in functions)
        {
            if (!map.TryAdd(function.Name, function))
            {
                throw new ArgumentException($"Duplicate Agent Framework tool name '{function.Name}'.", nameof(functions));
            }
        }

        return map;
    }

    private static DynamicToolCallParams ReadRequest(JsonElement? @params)
    {
        if (@params is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            throw new ArgumentException("Dynamic tool call request is missing params.", nameof(@params));
        }

        return @params.Value.Deserialize<DynamicToolCallParams>(SerializerOptions) ??
               throw new ArgumentException("Dynamic tool call request could not be parsed.", nameof(@params));
    }

    private static AIFunctionArguments CreateArguments(JsonElement arguments)
    {
        var aiArguments = new AIFunctionArguments();
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return aiArguments;
        }

        foreach (var property in arguments.EnumerateObject())
        {
            aiArguments[property.Name] = property.Value.Clone();
        }

        return aiArguments;
    }

    private static string FormatResult(object? value, JsonSerializerOptions serializerOptions)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            JsonElement json => FormatJsonElement(json),
            _ => JsonSerializer.Serialize(value, value.GetType(), serializerOptions)
        };
    }

    private static string FormatJsonElement(JsonElement json)
    {
        return json.ValueKind == JsonValueKind.String
            ? json.GetString() ?? string.Empty
            : json.GetRawText();
    }

    private static JsonElement CreateResponse(bool success, string text)
    {
        return JsonSerializer.SerializeToElement(
            new DynamicToolCallResponse
            {
                Success = success,
                ContentItems = [DynamicToolCallOutputContentItem.InputText(text)]
            },
            SerializerOptions);
    }
}
