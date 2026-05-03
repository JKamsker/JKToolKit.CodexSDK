using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.SemanticKernel;

namespace JKToolKit.CodexSDK.SemanticKernel;

/// <summary>
/// Handles Codex app-server dynamic tool calls by invoking Semantic Kernel functions.
/// </summary>
public sealed class SemanticKernelToolCallHandler : IAppServerApprovalHandler
{
    private const string DynamicToolCallMethod = "item/tool/call";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Kernel _kernel;
    private readonly IReadOnlyDictionary<string, KernelFunction> _functionsByToolName;
    private readonly IAppServerApprovalHandler? _fallbackHandler;

    /// <summary>
    /// Creates a handler for Codex app-server dynamic tool calls.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance used to invoke functions.</param>
    /// <param name="functions">The functions exposed as dynamic tools.</param>
    /// <param name="fallbackHandler">Optional handler for app-server requests not handled by this adapter.</param>
    public SemanticKernelToolCallHandler(
        Kernel kernel,
        IEnumerable<KernelFunction> functions,
        IAppServerApprovalHandler? fallbackHandler = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(functions);

        _kernel = kernel;
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
            return CreateResponse(success: false, $"Unknown Semantic Kernel tool '{request.Tool}'.");
        }

        try
        {
            var result = await function.InvokeAsync(_kernel, CreateArguments(function, request.Arguments), ct);
            return CreateResponse(success: true, FormatResult(result));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CreateResponse(success: false, ex.Message);
        }
    }

    private static IReadOnlyDictionary<string, KernelFunction> BuildFunctionMap(IEnumerable<KernelFunction> functions)
    {
        var map = new Dictionary<string, KernelFunction>(StringComparer.Ordinal);
        foreach (var function in functions)
        {
            var toolName = SemanticKernelCodexToolAdapter.GetToolName(function);
            if (!map.TryAdd(toolName, function))
            {
                throw new ArgumentException($"Duplicate Semantic Kernel tool name '{toolName}'.", nameof(functions));
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

    private static KernelArguments CreateArguments(KernelFunction function, JsonElement arguments)
    {
        var kernelArguments = new KernelArguments();
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return kernelArguments;
        }

        foreach (var parameter in function.Metadata.Parameters)
        {
            if (arguments.TryGetProperty(parameter.Name, out var value))
            {
                kernelArguments[parameter.Name] = DeserializeArgument(value, parameter.ParameterType);
            }
        }

        return kernelArguments;
    }

    private static object? DeserializeArgument(JsonElement value, Type? targetType)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (targetType is null || targetType == typeof(object) || targetType == typeof(JsonElement))
        {
            return value.Clone();
        }

        return value.Deserialize(targetType, SerializerOptions);
    }

    private static string FormatResult(FunctionResult result)
    {
        var value = result.GetValue<object?>();
        return value switch
        {
            null => string.Empty,
            string text => text,
            JsonElement json => json.GetRawText(),
            _ => JsonSerializer.Serialize(value, value.GetType(), SerializerOptions)
        };
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
