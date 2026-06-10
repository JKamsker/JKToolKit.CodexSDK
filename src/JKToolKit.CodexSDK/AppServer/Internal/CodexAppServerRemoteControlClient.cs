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

    public async Task<RemoteControlPairingStartResult> StartPairingAsync(RemoteControlPairingStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequireExperimentalApi("remoteControl/pairing/start");

        var result = await _sendRequestAsync(
            "remoteControl/pairing/start",
            new { manualCode = options.ManualCode },
            ct);

        return new RemoteControlPairingStartResult
        {
            PairingCode = CodexAppServerClientJson.GetRequiredString(result, "pairingCode", "remoteControl/pairing/start response"),
            ManualPairingCode = CodexAppServerClientJson.GetStringOrNull(result, "manualPairingCode"),
            EnvironmentId = CodexAppServerClientJson.GetRequiredString(result, "environmentId", "remoteControl/pairing/start response"),
            ExpiresAt = CodexAppServerClientJson.GetRequiredInt64(result, "expiresAt", "remoteControl/pairing/start response"),
            Raw = result
        };
    }

    public async Task<RemoteControlPairingStatusResult> ReadPairingStatusAsync(
        RemoteControlPairingStatusOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequireExperimentalApi("remoteControl/pairing/status");

        var hasPairingCode = !string.IsNullOrWhiteSpace(options.PairingCode);
        var hasManualPairingCode = !string.IsNullOrWhiteSpace(options.ManualPairingCode);
        if (hasPairingCode == hasManualPairingCode)
        {
            throw new ArgumentException(
                "Exactly one of PairingCode or ManualPairingCode must be provided.",
                nameof(options));
        }

        var result = await _sendRequestAsync(
            "remoteControl/pairing/status",
            new
            {
                pairingCode = hasPairingCode ? options.PairingCode : null,
                manualPairingCode = hasManualPairingCode ? options.ManualPairingCode : null
            },
            ct);

        return new RemoteControlPairingStatusResult
        {
            Claimed = CodexAppServerClientJson.GetRequiredBool(result, "claimed", "remoteControl/pairing/status response"),
            Raw = result
        };
    }

    public async Task<RemoteControlClientsListResult> ListClientsAsync(RemoteControlClientsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequireExperimentalApi("remoteControl/client/list");
        ValidateRequiredString(options.EnvironmentId, "EnvironmentId", nameof(options));
        if (options.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Limit, "Limit cannot be negative.");
        }

        var result = await _sendRequestAsync(
            "remoteControl/client/list",
            new
            {
                environmentId = options.EnvironmentId,
                cursor = options.Cursor,
                limit = options.Limit,
                order = options.Order?.Value
            },
            ct);

        return new RemoteControlClientsListResult
        {
            Clients = ParseClients(result),
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
            Raw = result
        };
    }

    public async Task<RemoteControlClientsRevokeResult> RevokeClientAsync(RemoteControlClientsRevokeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequireExperimentalApi("remoteControl/client/revoke");
        ValidateRequiredString(options.EnvironmentId, "EnvironmentId", nameof(options));
        ValidateRequiredString(options.ClientId, "ClientId", nameof(options));

        var result = await _sendRequestAsync(
            "remoteControl/client/revoke",
            new
            {
                environmentId = options.EnvironmentId,
                clientId = options.ClientId
            },
            ct);

        return new RemoteControlClientsRevokeResult
        {
            Raw = result
        };
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

    private static IReadOnlyList<RemoteControlClientInfo> ParseClients(JsonElement result)
    {
        var data = CodexAppServerClientJson.TryGetArray(result, "data");
        if (data is null)
        {
            return Array.Empty<RemoteControlClientInfo>();
        }

        var clients = new List<RemoteControlClientInfo>();
        foreach (var item in data.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var clientId = CodexAppServerClientJson.GetStringOrNull(item, "clientId");
            if (string.IsNullOrWhiteSpace(clientId))
            {
                continue;
            }

            clients.Add(new RemoteControlClientInfo
            {
                ClientId = clientId,
                DisplayName = CodexAppServerClientJson.GetStringOrNull(item, "displayName"),
                DeviceType = CodexAppServerClientJson.GetStringOrNull(item, "deviceType"),
                Platform = CodexAppServerClientJson.GetStringOrNull(item, "platform"),
                OsVersion = CodexAppServerClientJson.GetStringOrNull(item, "osVersion"),
                DeviceModel = CodexAppServerClientJson.GetStringOrNull(item, "deviceModel"),
                AppVersion = CodexAppServerClientJson.GetStringOrNull(item, "appVersion"),
                LastSeenAt = CodexAppServerClientJson.GetInt64OrNull(item, "lastSeenAt"),
                Raw = item.Clone()
            });
        }

        return clients;
    }

    private static void ValidateRequiredString(string? value, string displayName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }
    }
}
