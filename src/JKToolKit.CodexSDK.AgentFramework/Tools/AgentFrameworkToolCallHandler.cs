using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Tools;

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
    private readonly IServiceProvider? _functionInvocationServices;
    private readonly Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>>? _toolApprovalHandler;

    /// <summary>
    /// Creates a handler for Codex app-server dynamic tool calls.
    /// </summary>
    /// <param name="functions">The functions exposed as dynamic tools.</param>
    /// <param name="fallbackHandler">Optional handler for app-server requests not handled by this adapter.</param>
    public AgentFrameworkToolCallHandler(
        IEnumerable<AIFunction> functions,
        IAppServerApprovalHandler? fallbackHandler = null)
        : this(functions, fallbackHandler, functionInvocationServices: null, toolApprovalHandler: null)
    {
    }

    internal AgentFrameworkToolCallHandler(
        IEnumerable<AIFunction> functions,
        IAppServerApprovalHandler? fallbackHandler,
        IServiceProvider? functionInvocationServices = null,
        Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>>? toolApprovalHandler = null)
    {
        ArgumentNullException.ThrowIfNull(functions);

        _functionsByToolName = BuildFunctionMap(functions);
        _fallbackHandler = fallbackHandler;
        _functionInvocationServices = functionInvocationServices;
        _toolApprovalHandler = toolApprovalHandler;
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
            if (function.GetService<ApprovalRequiredAIFunction>() is not null)
            {
                var approval = await GetApprovalAsync(request, function, ct).ConfigureAwait(false);
                if (approval?.Approved != true)
                {
                    var reason = string.IsNullOrWhiteSpace(approval?.Reason)
                        ? $"Agent Framework tool '{request.Tool}' was not approved."
                        : approval.Reason;
                    return CreateResponse(success: false, reason);
                }
            }

            var arguments = CreateArguments(request, _functionInvocationServices);
            var result = await AgentFrameworkFunctionInvoker.InvokeAsync(
                    function,
                    arguments,
                    CreateCallContent(request, arguments),
                    ct)
                .ConfigureAwait(false);
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

    private async ValueTask<AgentFrameworkToolApprovalResponse?> GetApprovalAsync(
        DynamicToolCallParams request,
        AIFunction function,
        CancellationToken cancellationToken)
    {
        if (_toolApprovalHandler is null)
        {
            return null;
        }

        return await _toolApprovalHandler(
            new AgentFrameworkToolApprovalRequest(
                request.ThreadId,
                request.TurnId,
                request.CallId,
                function,
                request.Arguments),
            cancellationToken).ConfigureAwait(false);
    }

    private static AIFunctionArguments CreateArguments(
        DynamicToolCallParams request,
        IServiceProvider? functionInvocationServices)
    {
        var aiArguments = new AIFunctionArguments
        {
            Services = functionInvocationServices,
            Context = new Dictionary<object, object?>
            {
                ["codex.threadId"] = request.ThreadId,
                ["codex.turnId"] = request.TurnId,
                ["codex.callId"] = request.CallId,
                ["codex.toolName"] = request.Tool
            }
        };

        if (request.Arguments.ValueKind != JsonValueKind.Object)
        {
            return aiArguments;
        }

        foreach (var property in request.Arguments.EnumerateObject())
        {
            aiArguments[property.Name] = property.Value.Clone();
        }

        return aiArguments;
    }

    private static FunctionCallContent CreateCallContent(
        DynamicToolCallParams request,
        AIFunctionArguments arguments)
    {
        var callArguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            callArguments[argument.Key] = argument.Value;
        }

        return new FunctionCallContent(request.CallId, request.Tool, callArguments);
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
