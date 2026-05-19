using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerRemoteControlClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<bool> _experimentalApiEnabled;

    public CodexAppServerRemoteControlClient(
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<bool> experimentalApiEnabled)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _experimentalApiEnabled = experimentalApiEnabled ?? throw new ArgumentNullException(nameof(experimentalApiEnabled));
    }

    public async Task<RemoteControlStatusResult> EnableAsync(CancellationToken ct = default)
    {
        RequireExperimentalApi("remoteControl/enable");
        var result = await _sendRequestAsync("remoteControl/enable", new { }, ct);
        return ParseStatusResult(result, "remoteControl/enable response");
    }

    public async Task<RemoteControlStatusResult> DisableAsync(CancellationToken ct = default)
    {
        RequireExperimentalApi("remoteControl/disable");
        var result = await _sendRequestAsync("remoteControl/disable", new { }, ct);
        return ParseStatusResult(result, "remoteControl/disable response");
    }

    public async Task<RemoteControlStatusResult> ReadStatusAsync(CancellationToken ct = default)
    {
        RequireExperimentalApi("remoteControl/status/read");
        var result = await _sendRequestAsync("remoteControl/status/read", new { }, ct);
        return ParseStatusResult(result, "remoteControl/status/read response");
    }

    internal static RemoteControlStatusResult ParseStatusResult(JsonElement result, string context)
    {
        var status = CodexAppServerClientJson.GetRequiredString(result, "status", context);
        if (!RemoteControlConnectionStatus.TryParse(status, out var statusValue))
        {
            throw new InvalidOperationException($"{context} property 'status' is missing or invalid.");
        }

        return new RemoteControlStatusResult
        {
            Status = status,
            StatusValue = statusValue,
            ServerName = CodexAppServerClientJson.GetRequiredString(result, "serverName", context),
            InstallationId = CodexAppServerClientJson.GetRequiredString(result, "installationId", context),
            EnvironmentId = CodexAppServerClientJson.GetStringOrNull(result, "environmentId"),
            Raw = result
        };
    }

    private void RequireExperimentalApi(string descriptor)
    {
        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException(descriptor);
        }
    }
}
