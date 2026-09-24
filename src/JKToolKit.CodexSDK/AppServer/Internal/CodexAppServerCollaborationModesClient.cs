using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerCollaborationModesClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<bool> _experimentalApiEnabled;

    public CodexAppServerCollaborationModesClient(
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<bool> experimentalApiEnabled)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _experimentalApiEnabled = experimentalApiEnabled ?? throw new ArgumentNullException(nameof(experimentalApiEnabled));
    }

    public async Task<CollaborationModeListResult> ListCollaborationModesAsync(CancellationToken ct = default)
    {
        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException(AppServerMethods.CollaborationModeList);
        }

        var raw = await _sendRequestAsync(AppServerMethods.CollaborationModeList, new { }, ct);

        var masks = new List<CollaborationModeMask>();

        if (raw.ValueKind == JsonValueKind.Object &&
            raw.TryGetProperty(JsonFieldNames.Data, out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in data.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = TryGetString(m, JsonFieldNames.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                masks.Add(new CollaborationModeMask
                {
                    Name = name,
                    Mode = TryGetString(m, JsonFieldNames.Mode),
                    Model = TryGetString(m, JsonFieldNames.Model),
                    ReasoningEffort = TryGetString(m, JsonFieldNames.SnakeCase.ReasoningEffort) ?? TryGetString(m, JsonFieldNames.ReasoningEffort),
                    Raw = m.Clone()
                });
            }
        }

        return new CollaborationModeListResult
        {
            Data = masks,
            Raw = raw
        };
    }

    private static string? TryGetString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!obj.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return prop.GetString();
    }
}
