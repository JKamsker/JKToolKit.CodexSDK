using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerClientCore
{
    public async Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
    {
        try
        {
            var observers = _options.MessageObservers;
            var requestTransformers = _options.RequestParamsTransformers;
            var paramsToSend = @params;
            JsonElement? observedParams = null;
            if (requestTransformers is { Count: > 0 })
            {
                var transformed = SerializeToElementOrEmptyObject(@params);

                foreach (var transformer in requestTransformers)
                {
                    if (transformer is null)
                    {
                        continue;
                    }

                    try
                    {
                        transformed = transformer.Transform(method, transformed);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "App-server request params transformer threw (method={Method}).", method);
                    }
                }

                paramsToSend = transformed;
                observedParams = transformed;
            }

            if (observers is { Count: > 0 })
            {
                observedParams ??= SerializeToElementOrEmptyObject(@params);

                foreach (var observer in observers)
                {
                    if (observer is null)
                    {
                        continue;
                    }

                    try
                    {
                        observer.OnRequest(method, observedParams.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "App-server message observer threw in OnRequest (method={Method}).", method);
                    }
                }
            }

            var result = await _rpc.SendRequestAsync(method, paramsToSend, ct);

            var responseTransformers = _options.ResponseTransformers;
            if (responseTransformers is { Count: > 0 })
            {
                foreach (var transformer in responseTransformers)
                {
                    if (transformer is null)
                    {
                        continue;
                    }

                    try
                    {
                        result = transformer.Transform(method, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "App-server response transformer threw (method={Method}).", method);
                    }
                }
            }

            if (observers is { Count: > 0 })
            {
                foreach (var observer in observers)
                {
                    if (observer is null)
                    {
                        continue;
                    }

                    try
                    {
                        observer.OnResponse(method, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "App-server message observer threw in OnResponse (method={Method}).", method);
                    }
                }
            }

            return result;
        }
        catch (JsonRpcRemoteException ex) when (CodexAppServerClient.TryParseExperimentalApiRequiredMessage(ex.Error.Message, out var descriptor))
        {
            throw new CodexExperimentalApiRequiredException(descriptor, ex);
        }
    }

    private JsonElement SerializeToElementOrEmptyObject(object? value)
    {
        if (value is null)
        {
            return EmptyObject;
        }

        if (value is JsonElement je)
        {
            return je;
        }

        if (value is JsonDocument doc)
        {
            return doc.RootElement.Clone();
        }

        var options = _options.SerializerOptionsOverride ?? DefaultSerializerOptions;
        return JsonSerializer.SerializeToElement(value, options);
    }
}
