using System.Text.Json;

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
            throw new CodexExperimentalApiRequiredException("collaborationMode/list");
        }

        var raw = await _sendRequestAsync("collaborationMode/list", new { }, ct);

        var masks = new List<CollaborationModeMask>();

        if (raw.ValueKind == JsonValueKind.Object &&
            raw.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in data.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = TryGetString(m, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                masks.Add(new CollaborationModeMask
                {
                    Name = name,
                    Mode = TryGetString(m, "mode"),
                    Model = TryGetString(m, "model"),
                    ReasoningEffort = TryGetString(m, "reasoning_effort") ?? TryGetString(m, "reasoningEffort"),
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
